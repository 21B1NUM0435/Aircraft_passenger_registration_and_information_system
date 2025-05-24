using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using FlightManagementSystem.Web.Models.Api;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FlightManagementSystem.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlightsController : ControllerBase
    {
        private readonly IFlightService _flightService;
        private readonly ISocketServer _socketServer;
        private readonly IFlightHubService? _flightHubService;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(
            IFlightService flightService,
            ISocketServer socketServer,
            ILogger<FlightsController> logger,
            IFlightHubService? flightHubService = null)
        {
            _flightService = flightService;
            _socketServer = socketServer;
            _logger = logger;
            _flightHubService = flightHubService;
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
            _logger.LogInformation("🔄 Received flight status update request: {FlightNumber} -> {NewStatus}",
                flightNumber, updateDto.NewStatus);

            if (!Enum.TryParse<FlightStatus>(updateDto.NewStatus, true, out var newStatus))
            {
                _logger.LogWarning("❌ Invalid flight status: {NewStatus}", updateDto.NewStatus);
                return BadRequest($"Invalid flight status: {updateDto.NewStatus}");
            }

            var flight = await _flightService.GetFlightByNumberAsync(flightNumber);
            if (flight == null)
            {
                _logger.LogWarning("❌ Flight not found: {FlightNumber}", flightNumber);
                return NotFound();
            }

            var success = await _flightService.UpdateFlightStatusAsync(flightNumber, newStatus);
            if (!success)
            {
                _logger.LogError("❌ Failed to update flight status in database: {FlightNumber}", flightNumber);
                return BadRequest("Failed to update flight status");
            }

            _logger.LogInformation("✅ Successfully updated flight status in database: {FlightNumber} -> {Status}",
                flightNumber, newStatus);

            // CRITICAL: Broadcast to BOTH Socket Server AND SignalR Hub
            await BroadcastFlightStatusUpdate(flightNumber, newStatus);

            return Ok(new
            {
                status = "Flight status updated successfully",
                flightNumber = flightNumber,
                newStatus = newStatus.ToString(),
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Broadcasts flight status update to ALL clients (Windows apps via Socket + Web clients via SignalR)
        /// </summary>
        private async Task BroadcastFlightStatusUpdate(string flightNumber, FlightStatus newStatus)
        {
            _logger.LogInformation("📡 Broadcasting flight status update: {FlightNumber} -> {Status}", flightNumber, newStatus);

            var broadcastTasks = new List<Task>();

            // 1. Broadcast to Socket Server (Windows applications)
            broadcastTasks.Add(Task.Run(() => BroadcastToSocketServer(flightNumber, newStatus)));

            // 2. Broadcast to SignalR Hub (Web clients)
            if (_flightHubService != null)
            {
                broadcastTasks.Add(BroadcastToSignalRHub(flightNumber, newStatus));
            }
            else
            {
                _logger.LogWarning("⚠️ FlightHubService is null - SignalR broadcast skipped");
            }

            // Wait for all broadcasts to complete
            try
            {
                await Task.WhenAll(broadcastTasks);
                _logger.LogInformation("✅ All flight status broadcasts completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during flight status broadcasting");
            }
        }

        /// <summary>
        /// Broadcast flight status update to Socket Server (Windows apps)
        /// </summary>
        private void BroadcastToSocketServer(string flightNumber, FlightStatus newStatus)
        {
            try
            {
                var socketMessage = new
                {
                    type = "FlightStatusUpdate",
                    data = new
                    {
                        flightNumber = flightNumber,
                        newStatus = newStatus.ToString(),
                        timestamp = DateTime.UtcNow
                    },
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(socketMessage);
                _socketServer.BroadcastMessage(json);

                _logger.LogInformation("📡 Socket Server broadcast sent: {FlightNumber} -> {Status}", flightNumber, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to broadcast via Socket Server for flight {FlightNumber}", flightNumber);
            }
        }

        /// <summary>
        /// Broadcast flight status update to SignalR Hub (Web clients)
        /// </summary>
        private async Task BroadcastToSignalRHub(string flightNumber, FlightStatus newStatus)
        {
            try
            {
                await _flightHubService!.NotifyFlightStatusChanged(flightNumber, newStatus);
                _logger.LogInformation("📡 SignalR Hub broadcast sent: {FlightNumber} -> {Status}", flightNumber, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to broadcast via SignalR for flight {FlightNumber}", flightNumber);
            }
        }
    }
}