using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Web.Models.Api;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class CheckInController : ControllerBase
    {
        private readonly ICheckInService _checkInService;
        private readonly IBoardingPassService _boardingPassService;
        private readonly ISocketServer _socketServer;
        private readonly IFlightHubService? _flightHubService;
        private readonly ILogger<CheckInController> _logger;

        // Static dictionary to track ongoing check-in processes per seat
        private static readonly ConcurrentDictionary<string, DateTime> _ongoingCheckIns = new();

        public CheckInController(
            ICheckInService checkInService,
            IBoardingPassService boardingPassService,
            ISocketServer socketServer,
            ILogger<CheckInController> logger,
            IFlightHubService? flightHubService = null)
        {
            _checkInService = checkInService;
            _boardingPassService = boardingPassService;
            _socketServer = socketServer;
            _logger = logger;
            _flightHubService = flightHubService;
        }

        // GET: api/checkin/search
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BookingDto>> SearchBooking([FromQuery] string passportNumber, [FromQuery] string flightNumber)
        {
            if (string.IsNullOrWhiteSpace(passportNumber) || string.IsNullOrWhiteSpace(flightNumber))
            {
                return BadRequest("Passport number and flight number are required");
            }

            try
            {
                var booking = await _checkInService.FindBookingByPassportAsync(passportNumber, flightNumber);
                if (booking == null)
                {
                    return NotFound("No booking found for the provided passport number and flight");
                }

                var bookingDto = new BookingDto
                {
                    BookingReference = booking.BookingReference,
                    PassengerId = booking.PassengerId,
                    FlightNumber = booking.FlightNumber,
                    PassengerName = $"{booking.Passenger.FirstName} {booking.Passenger.LastName}",
                    CheckedIn = booking.CheckInStatus == Core.Models.CheckInStatus.CheckedIn
                };

                return Ok(bookingDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching booking for passport {PassportNumber} on flight {FlightNumber}",
                    passportNumber, flightNumber);
                return StatusCode(500, "An error occurred while searching for the booking");
            }
        }

        // GET: api/checkin/available-seats/{flightNumber}
        [HttpGet("available-seats/{flightNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<SeatDto>>> GetAvailableSeats(string flightNumber)
        {
            try
            {
                var availableSeats = await _checkInService.GetAvailableSeatsAsync(flightNumber);

                var seatDtos = availableSeats.Select(s => new SeatDto
                {
                    SeatId = s.SeatId,
                    SeatNumber = s.SeatNumber,
                    SeatClass = s.SeatClass.ToString(),
                    Price = s.Price
                });

                return Ok(seatDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available seats for flight {FlightNumber}", flightNumber);
                return StatusCode(500, "An error occurred while retrieving available seats");
            }
        }

        // POST: api/checkin/process
        [HttpPost("process")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status423Locked)]
        public async Task<ActionResult<CheckInResponseDto>> ProcessCheckIn([FromBody] CheckInRequestDto request)
        {
            _logger.LogInformation("Processing check-in request for booking {BookingReference}, seat {SeatId}",
                request.BookingReference, request.SeatId);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if this seat is already being processed
            if (_ongoingCheckIns.ContainsKey(request.SeatId))
            {
                var processingTime = _ongoingCheckIns[request.SeatId];
                if ((DateTime.UtcNow - processingTime).TotalSeconds < 30)
                {
                    _logger.LogWarning("Seat {SeatId} is already being processed", request.SeatId);
                    return StatusCode(423, new { error = "This seat is currently being processed by another staff member. Please wait or select a different seat." });
                }
                else
                {
                    _ongoingCheckIns.TryRemove(request.SeatId, out _);
                }
            }

            // Mark seat as being processed
            _ongoingCheckIns[request.SeatId] = DateTime.UtcNow;
            _logger.LogInformation("Marked seat {SeatId} as being processed", request.SeatId);

            try
            {
                // Immediately broadcast seat lock to prevent race conditions
                BroadcastSeatLock(request.SeatId, request.FlightNumber, true);

                // Verify seat is still available (double-check)
                var isSeatAvailable = await _checkInService.IsSeatAvailableAsync(request.SeatId, request.FlightNumber);
                if (!isSeatAvailable)
                {
                    _logger.LogWarning("Seat {SeatId} is no longer available", request.SeatId);
                    return Conflict(new { error = "The selected seat is no longer available" });
                }

                // Process check-in
                var result = await _checkInService.CheckInPassengerAsync(
                    request.BookingReference,
                    request.SeatId,
                    request.StaffId,
                    request.CounterId);

                if (!result.Success)
                {
                    _logger.LogWarning("Check-in failed for booking {BookingReference}: {Message}",
                        request.BookingReference, result.Message);
                    return BadRequest(new { error = result.Message });
                }

                // Generate boarding pass PDF
                byte[] boardingPassPdf = Array.Empty<byte>();
                if (result.BoardingPass != null)
                {
                    boardingPassPdf = await _boardingPassService.GenerateBoardingPassPdfAsync(result.BoardingPass);

                    // Broadcast successful check-in to all clients
                    BroadcastCheckInComplete(request.FlightNumber, request.BookingReference,
                        request.PassengerName, request.SeatId);
                }

                _logger.LogInformation("Check-in completed successfully for booking {BookingReference}, seat {SeatId}",
                    request.BookingReference, request.SeatId);

                return Ok(new CheckInResponseDto
                {
                    Success = true,
                    Message = result.Message,
                    BoardingPassId = result.BoardingPass?.BoardingPassId,
                    BoardingPassPdf = boardingPassPdf
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing check-in for booking {BookingReference}", request.BookingReference);
                return StatusCode(500, new { error = "An error occurred while processing the check-in" });
            }
            finally
            {
                // Always remove from ongoing check-ins and broadcast unlock
                _ongoingCheckIns.TryRemove(request.SeatId, out _);
                BroadcastSeatLock(request.SeatId, request.FlightNumber, false);
                _logger.LogInformation("Released seat {SeatId} from processing", request.SeatId);
            }
        }

        // GET: api/checkin/boarding-pass/{bookingReference}
        [HttpGet("boarding-pass/{bookingReference}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetBoardingPass(string bookingReference)
        {
            try
            {
                var boardingPass = await _boardingPassService.GetBoardingPassAsync(bookingReference);
                if (boardingPass == null)
                {
                    return NotFound("No boarding pass found for the provided booking reference");
                }

                var boardingPassPdf = await _boardingPassService.GenerateBoardingPassPdfAsync(boardingPass);
                return File(boardingPassPdf, "application/pdf", $"BoardingPass_{bookingReference}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving boarding pass for booking {BookingReference}", bookingReference);
                return StatusCode(500, "An error occurred while retrieving the boarding pass");
            }
        }

        // SINGLE BroadcastSeatLock method (not async)
        private void BroadcastSeatLock(string seatId, string flightNumber, bool isLocked)
        {
            _logger.LogInformation("Broadcasting seat lock: {SeatId} = {IsLocked} for flight {FlightNumber}",
                seatId, isLocked, flightNumber);

            var message = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "SeatLock",
                data = new
                {
                    seatId = seatId,
                    flightNumber = flightNumber,
                    isLocked = isLocked,
                    timestamp = DateTime.UtcNow
                }
            });

            // Broadcast to Windows applications via Socket Server
            _socketServer.BroadcastMessage(message);
            _logger.LogInformation("Sent seat lock message via Socket Server");

            // Broadcast to Web clients via SignalR Hub (if available)
            if (_flightHubService != null)
            {
                try
                {
                    // Fire and forget for SignalR (don't await)
                    _ = Task.Run(async () => await _flightHubService.NotifySeatAssigned(flightNumber, seatId, isLocked));
                    _logger.LogInformation("Sent seat lock message via SignalR");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send seat lock via SignalR");
                }
            }
        }

        // SINGLE BroadcastCheckInComplete method (not async)
        private void BroadcastCheckInComplete(string flightNumber, string bookingReference,
            string passengerName, string seatId)
        {
            _logger.LogInformation("Broadcasting check-in complete for seat {SeatId} on flight {FlightNumber}",
                seatId, flightNumber);

            // Broadcast seat assignment to Socket Server
            var seatMessage = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "SeatAssignment",
                data = new
                {
                    flightNumber = flightNumber,
                    seatId = seatId,
                    isAssigned = true,
                    passengerName = passengerName,
                    timestamp = DateTime.UtcNow
                }
            });

            _socketServer.BroadcastMessage(seatMessage);
            _logger.LogInformation("Sent seat assignment message via Socket Server");

            // Broadcast check-in complete
            var checkInMessage = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "CheckInComplete",
                data = new
                {
                    flightNumber = flightNumber,
                    bookingReference = bookingReference,
                    passengerName = passengerName,
                    seatId = seatId,
                    timestamp = DateTime.UtcNow
                }
            });

            _socketServer.BroadcastMessage(checkInMessage);
            _logger.LogInformation("Sent check-in complete message via Socket Server");

            // SignalR notifications to web clients (fire and forget)
            if (_flightHubService != null)
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        await _flightHubService.NotifySeatAssigned(flightNumber, seatId, true);
                        await _flightHubService.NotifyPassengerCheckedIn(flightNumber, passengerName);
                    });
                    _logger.LogInformation("Sent SignalR notifications successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send SignalR notifications");
                }
            }
            else
            {
                _logger.LogWarning("FlightHubService is null - no SignalR notifications sent");
            }
        }
    }
}