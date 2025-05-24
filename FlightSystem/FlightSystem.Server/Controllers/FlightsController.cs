using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using FlightSystem.Server.Hubs;

namespace FlightSystem.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightsController : ControllerBase
{
    private readonly FlightDbContext _context;
    private readonly IHubContext<FlightHub> _hubContext;
    private readonly ILogger<FlightsController> _logger;

    public FlightsController(FlightDbContext context, IHubContext<FlightHub> hubContext, ILogger<FlightsController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    // GET: api/flights
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Flight>>> GetFlights()
    {
        var flights = await _context.Flights
            .Include(f => f.Seats)
            .Include(f => f.Bookings)
            .ToListAsync();

        return Ok(flights);
    }

    // GET: api/flights/{flightNumber}
    [HttpGet("{flightNumber}")]
    public async Task<ActionResult<Flight>> GetFlight(string flightNumber)
    {
        var flight = await _context.Flights
            .Include(f => f.Seats)
            .Include(f => f.Bookings)
            .ThenInclude(b => b.Passenger)
            .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);

        if (flight == null)
        {
            return NotFound();
        }

        return Ok(flight);
    }

    // PUT: api/flights/{flightNumber}/status
    [HttpPut("{flightNumber}/status")]
    public async Task<IActionResult> UpdateFlightStatus(string flightNumber, [FromBody] FlightStatus newStatus)
    {
        _logger.LogInformation("🔄 Updating flight {FlightNumber} status to {Status}", flightNumber, newStatus);

        var flight = await _context.Flights.FindAsync(flightNumber);
        if (flight == null)
        {
            return NotFound();
        }

        var oldStatus = flight.Status;
        flight.Status = newStatus;

        await _context.SaveChangesAsync();

        // Broadcast to all connected clients via SignalR
        await _hubContext.Clients.All.SendAsync("FlightStatusChanged", new
        {
            FlightNumber = flightNumber,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("✅ Flight {FlightNumber} status updated: {OldStatus} → {NewStatus}",
            flightNumber, oldStatus, newStatus);

        return Ok(new
        {
            message = "Flight status updated successfully",
            flightNumber,
            oldStatus = oldStatus.ToString(),
            newStatus = newStatus.ToString()
        });
    }

    // GET: api/flights/{flightNumber}/available-seats
    [HttpGet("{flightNumber}/available-seats")]
    public async Task<ActionResult<IEnumerable<Seat>>> GetAvailableSeats(string flightNumber)
    {
        var seats = await _context.Seats
            .Where(s => s.FlightNumber == flightNumber && s.IsAvailable)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync();

        return Ok(seats);
    }
}