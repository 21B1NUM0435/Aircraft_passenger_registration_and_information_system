using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using FlightSystem.Server.Models.DTOs;
using FlightSystem.Server.Hubs;
using FlightSystem.Server.Services;

namespace FlightSystem.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightsController : ControllerBase
{
    private readonly IFlightService _flightService;
    private readonly IHubContext<FlightHub> _hubContext;
    private readonly ILogger<FlightsController> _logger;
    private readonly FlightDbContext _context;

    public FlightsController(IFlightService flightService, IHubContext<FlightHub> hubContext, ILogger<FlightsController> logger, FlightDbContext context)
    {
        _flightService = flightService;
        _hubContext = hubContext;
        _logger = logger;
        _context = context;
    }

    // GET: api/flights
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FlightDto>>> GetFlights()
    {
        try
        {
            _logger.LogInformation("Getting all flights");

            var flights = await _flightService.GetAllFlightsAsync();
            var flightDtos = flights.Select(FlightDto.FromFlight).ToList();

            _logger.LogInformation("Retrieved {Count} flights", flightDtos.Count);
            return Ok(flightDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving flights");
            return StatusCode(500, new { message = "Error retrieving flights", error = ex.Message });
        }
    }

    // GET: api/flights/{flightNumber}
    [HttpGet("{flightNumber}")]
    public async Task<ActionResult<FlightDto>> GetFlight(string flightNumber)
    {
        try
        {
            var flight = await _flightService.GetFlightAsync(flightNumber);

            if (flight == null)
            {
                return NotFound(new { message = $"Flight {flightNumber} not found" });
            }

            return Ok(FlightDto.FromFlight(flight));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving flight {FlightNumber}", flightNumber);
            return StatusCode(500, new { message = "Error retrieving flight", error = ex.Message });
        }
    }

    // PUT: api/flights/{flightNumber}/status
    [HttpPut("{flightNumber}/status")]
    public async Task<IActionResult> UpdateFlightStatus(string flightNumber, [FromBody] string newStatusJson)
    {
        try
        {
            _logger.LogInformation("Updating flight {FlightNumber} status to {Status}", flightNumber, newStatusJson);

            // Parse the status from JSON string (since it comes as a JSON-serialized string)
            var statusString = System.Text.Json.JsonSerializer.Deserialize<string>(newStatusJson);

            if (!Enum.TryParse<FlightStatus>(statusString, out var newStatus))
            {
                return BadRequest(new { message = $"Invalid flight status: {statusString}" });
            }

            var flight = await _flightService.GetFlightAsync(flightNumber);
            if (flight == null)
            {
                return NotFound(new { message = $"Flight {flightNumber} not found" });
            }

            var oldStatus = flight.Status;
            var success = await _flightService.UpdateFlightStatusAsync(flightNumber, newStatus);

            if (!success)
            {
                return BadRequest(new { message = "Failed to update flight status" });
            }

            // Broadcast to all connected clients via SignalR
            await _hubContext.Clients.All.SendAsync("FlightStatusChanged", new
            {
                FlightNumber = flightNumber,
                OldStatus = oldStatus.ToString(),
                NewStatus = newStatus.ToString(),
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Flight {FlightNumber} status updated: {OldStatus} → {NewStatus}",
                flightNumber, oldStatus, newStatus);

            return Ok(new
            {
                message = "Flight status updated successfully",
                flightNumber,
                oldStatus = oldStatus.ToString(),
                newStatus = newStatus.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating flight status for {FlightNumber}", flightNumber);
            return StatusCode(500, new { message = "Error updating flight status", error = ex.Message });
        }
    }

    // GET: api/flights/{flightNumber}/available-seats
    [HttpGet("{flightNumber}/available-seats")]
    public async Task<ActionResult<IEnumerable<SeatDto>>> GetAvailableSeats(string flightNumber)
    {
        try
        {
            var seats = await _flightService.GetAvailableSeatsAsync(flightNumber);
            var seatDtos = seats.Select(SeatDto.FromSeat).ToList();

            return Ok(seatDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available seats for flight {FlightNumber}", flightNumber);
            return StatusCode(500, new { message = "Error retrieving available seats", error = ex.Message });
        }
    }


    [HttpGet("debug/database-stats")]
    public async Task<ActionResult> GetDatabaseStats()
    {
        try
        {
            var stats = new
            {
                AircraftCount = await _context.Aircraft.CountAsync(),
                FlightCount = await _context.Flights.CountAsync(),
                PassengerCount = await _context.Passengers.CountAsync(),
                BookingCount = await _context.Bookings.CountAsync(),
                TotalSeats = await _context.Seats.CountAsync(),
                AvailableSeats = await _context.Seats.CountAsync(s => s.IsAvailable),

                // Detailed breakdown
                FlightDetails = await _context.Flights
                    .Select(f => new
                    {
                        f.FlightNumber,
                        f.Origin,
                        f.Destination,
                        f.Status,
                        AircraftModel = f.Aircraft != null ? f.Aircraft.Model : "No Aircraft",
                        TotalSeats = f.Seats.Count(),
                        AvailableSeats = f.Seats.Count(s => s.IsAvailable),
                        BookingCount = f.Bookings.Count()
                    })
                    .ToListAsync(),

                // Sample seats from each flight
                SeatSamples = await _context.Seats
                    .GroupBy(s => s.FlightNumber)
                    .Select(g => new
                    {
                        FlightNumber = g.Key,
                        SampleSeats = g.Take(5).Select(s => new
                        {
                            s.SeatNumber,
                            s.Class,
                            s.IsAvailable,
                            s.Price
                        }).ToList()
                    })
                    .ToListAsync()
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpGet("debug/raw-query")]
    public async Task<ActionResult> RunRawQuery()
    {
        try
        {
            // Raw SQL query to check data
            var result = await _context.Database.SqlQueryRaw<object>(
                @"SELECT 
                f.FlightNumber,
                COUNT(s.SeatId) as TotalSeats,
                SUM(CASE WHEN s.IsAvailable = 1 THEN 1 ELSE 0 END) as AvailableSeats
              FROM Flights f
              LEFT JOIN Seats s ON f.FlightNumber = s.FlightNumber
              GROUP BY f.FlightNumber"
            ).ToListAsync();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}