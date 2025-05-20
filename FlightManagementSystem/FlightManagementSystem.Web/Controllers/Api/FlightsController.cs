using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using FlightManagementSystem.Web.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace FlightManagementSystem.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlightsController : ControllerBase
    {
        private readonly IFlightService _flightService;
        private readonly ISocketServer _socketServer;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(
            IFlightService flightService,
            ISocketServer socketServer,
            ILogger<FlightsController> logger)
        {
            _flightService = flightService;
            _socketServer = socketServer;
            _logger = logger;
        }

        // GET: api/flights
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<FlightDto>>> GetFlights()
        {
            var flights = await _flightService.GetAllFlightsAsync();
            var flightDtos = flights.Select(f => new FlightDto
            {
                FlightNumber = f.FlightNumber,
                Origin = f.Origin,
                Destination = f.Destination,
                DepartureTime = f.DepartureTime,
                ArrivalTime = f.ArrivalTime,
                Status = f.Status.ToString(),
                AircraftModel = f.Aircraft?.Model ?? "Unknown"
            });

            return Ok(flightDtos);
        }

        // GET: api/flights/{flightNumber}
        [HttpGet("{flightNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FlightDetailDto>> GetFlight(string flightNumber)
        {
            var flight = await _flightService.GetFlightByNumberAsync(flightNumber);
            if (flight == null)
            {
                return NotFound();
            }

            var flightDetail = new FlightDetailDto
            {
                FlightNumber = flight.FlightNumber,
                Origin = flight.Origin,
                Destination = flight.Destination,
                DepartureTime = flight.DepartureTime,
                ArrivalTime = flight.ArrivalTime,
                Status = flight.Status.ToString(),
                AircraftModel = flight.Aircraft?.Model ?? "Unknown",
                AircraftId = flight.AircraftId
            };

            return Ok(flightDetail);
        }

        // GET: api/flights/{flightNumber}/passengers
        [HttpGet("{flightNumber}/passengers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<PassengerDto>>> GetFlightPassengers(string flightNumber)
        {
            var flight = await _flightService.GetFlightByNumberAsync(flightNumber);
            if (flight == null)
            {
                return NotFound();
            }

            var passengers = await _flightService.GetCheckedInPassengersAsync(flightNumber);
            var passengerDtos = passengers.Select(p => new PassengerDto
            {
                PassengerId = p.PassengerId,
                FirstName = p.FirstName,
                LastName = p.LastName,
                PassportNumber = p.PassportNumber
            });

            return Ok(passengerDtos);
        }

        // PATCH: api/flights/{flightNumber}/status
        [HttpPatch("{flightNumber}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFlightStatus(string flightNumber, [FromBody] UpdateFlightStatusDto updateDto)
        {
            if (!Enum.TryParse<FlightStatus>(updateDto.NewStatus, true, out var newStatus))
            {
                return BadRequest($"Invalid flight status: {updateDto.NewStatus}");
            }

            var flight = await _flightService.GetFlightByNumberAsync(flightNumber);
            if (flight == null)
            {
                return NotFound();
            }

            var success = await _flightService.UpdateFlightStatusAsync(flightNumber, newStatus);
            if (!success)
            {
                return BadRequest("Failed to update flight status");
            }

            // Broadcast flight status update to connected clients
            _socketServer.BroadcastMessage(System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "FlightStatusUpdate",
                data = new
                {
                    flightNumber,
                    newStatus = newStatus.ToString()
                },
                timestamp = DateTime.UtcNow
            }));

            _logger.LogInformation("Flight {FlightNumber} status updated to {Status}", flightNumber, newStatus);
            return Ok(new { status = "Flight status updated successfully" });
        }
    }
}