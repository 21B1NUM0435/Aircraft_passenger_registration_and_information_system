using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Web.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace FlightManagementSystem.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class CheckInController : ControllerBase
    {
        private readonly ICheckInService _checkInService;
        private readonly IBoardingPassService _boardingPassService;
        private readonly ISocketServer _socketServer;
        private readonly ILogger<CheckInController> _logger;

        public CheckInController(
            ICheckInService checkInService,
            IBoardingPassService boardingPassService,
            ISocketServer socketServer,
            ILogger<CheckInController> logger)
        {
            _checkInService = checkInService;
            _boardingPassService = boardingPassService;
            _socketServer = socketServer;
            _logger = logger;
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

        // GET: api/checkin/available-seats/{flightNumber}
        [HttpGet("available-seats/{flightNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<SeatDto>>> GetAvailableSeats(string flightNumber)
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

        // POST: api/checkin/process
        [HttpPost("process")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CheckInResponseDto>> ProcessCheckIn([FromBody] CheckInRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verify seat is available (first check to avoid race conditions)
            var isSeatAvailable = await _checkInService.IsSeatAvailableAsync(request.SeatId, request.FlightNumber);
            if (!isSeatAvailable)
            {
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
                return BadRequest(new { error = result.Message });
            }

            // Broadcast seat assignment to connected clients
            _socketServer.BroadcastMessage(System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "SeatAssignment",
                data = new
                {
                    flightNumber = request.FlightNumber,
                    seatId = request.SeatId,
                    isAssigned = true
                },
                timestamp = DateTime.UtcNow
            }));

            // Generate boarding pass PDF
            byte[] boardingPassPdf = new byte[0];
            if (result.BoardingPass != null)
            {
                boardingPassPdf = await _boardingPassService.GenerateBoardingPassPdfAsync(result.BoardingPass);

                // Broadcast check-in complete
                _socketServer.BroadcastMessage(System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "CheckInComplete",
                    data = new
                    {
                        flightNumber = request.FlightNumber,
                        bookingReference = request.BookingReference,
                        passengerName = request.PassengerName
                    },
                    timestamp = DateTime.UtcNow
                }));
            }

            _logger.LogInformation("Passenger with booking {BookingReference} checked in for flight {FlightNumber}",
                request.BookingReference, request.FlightNumber);

            return Ok(new CheckInResponseDto
            {
                Success = true,
                Message = result.Message,
                BoardingPassId = result.BoardingPass?.BoardingPassId,
                BoardingPassPdf = boardingPassPdf
            });
        }

        // GET: api/checkin/boarding-pass/{bookingReference}
        [HttpGet("boarding-pass/{bookingReference}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<byte[]>> GetBoardingPass(string bookingReference)
        {
            var boardingPass = await _boardingPassService.GetBoardingPassAsync(bookingReference);
            if (boardingPass == null)
            {
                return NotFound("No boarding pass found for the provided booking reference");
            }

            var boardingPassPdf = await _boardingPassService.GenerateBoardingPassPdfAsync(boardingPass);
            return File(boardingPassPdf, "application/pdf", $"BoardingPass_{bookingReference}.pdf");
        }
    }
}