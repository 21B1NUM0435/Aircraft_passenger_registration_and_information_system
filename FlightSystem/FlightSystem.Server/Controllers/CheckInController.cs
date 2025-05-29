using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using FlightSystem.Server.Hubs;
using FlightSystem.Server.Services;

namespace FlightSystem.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckInController : ControllerBase
{
    private readonly FlightDbContext _context;
    private readonly IHubContext<FlightHub> _hubContext;
    private readonly IConcurrencyService _concurrencyService;
    private readonly ILogger<CheckInController> _logger;

    public CheckInController(
        FlightDbContext context,
        IHubContext<FlightHub> hubContext,
        IConcurrencyService concurrencyService,
        ILogger<CheckInController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _concurrencyService = concurrencyService;
        _logger = logger;
    }

    // POST: api/checkin/search
    [HttpPost("search")]
    public async Task<ActionResult> SearchPassenger([FromBody] SearchRequest request)
    {
        _logger.LogInformation("🔍 Searching for passenger: {PassportNumber} on flight {FlightNumber}",
            request.PassportNumber, request.FlightNumber);

        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Include(b => b.Seat)
                .FirstOrDefaultAsync(b => b.PassportNumber == request.PassportNumber
                                       && b.FlightNumber == request.FlightNumber);

            if (booking == null)
            {
                _logger.LogWarning("❌ No booking found for passport {PassportNumber} on flight {FlightNumber}",
                    request.PassportNumber, request.FlightNumber);
                return NotFound(new { message = "No booking found for this passport and flight" });
            }

            _logger.LogInformation("✅ Found booking {BookingReference} for passenger {PassengerName}",
                booking.BookingReference, booking.Passenger.FullName);

            return Ok(new
            {
                BookingReference = booking.BookingReference,
                PassengerName = booking.Passenger.FullName,
                FlightNumber = booking.FlightNumber,
                Status = booking.Status.ToString(),
                AssignedSeat = booking.Seat?.SeatNumber,
                CheckInTime = booking.CheckInTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error searching for passenger {PassportNumber} on flight {FlightNumber}",
                request.PassportNumber, request.FlightNumber);
            return StatusCode(500, new { message = "Internal server error during passenger search" });
        }
    }

    // POST: api/checkin/assign-seat
    [HttpPost("assign-seat")]
    public async Task<ActionResult> AssignSeat([FromBody] AssignSeatRequest request)
    {
        _logger.LogInformation("🪑 Starting seat assignment: Seat {SeatId} to Booking {BookingReference} by {StaffName}",
            request.SeatId, request.BookingReference, request.StaffName ?? "Unknown");

        try
        {
            // Use enhanced concurrency service
            var result = await _concurrencyService.AssignSeatWithConcurrencyCheckAsync(
                request.SeatId,
                request.BookingReference,
                request.StaffName ?? Environment.UserName);

            if (!result.IsSuccess)
            {
                var statusCode = result.ErrorType switch
                {
                    SeatAssignmentErrorType.NotFound => 404,
                    SeatAssignmentErrorType.Conflict => 409, // Conflict
                    _ => 400
                };

                _logger.LogWarning("⚠️ Seat assignment failed: {ErrorMessage} (Type: {ErrorType})",
                    result.ErrorMessage, result.ErrorType);

                return StatusCode(statusCode, new
                {
                    message = result.ErrorMessage,
                    errorType = result.ErrorType.ToString(),
                    timestamp = DateTime.UtcNow
                });
            }

            var assignmentInfo = result.Data!;

            // Broadcast successful assignment to all clients
            await _hubContext.Clients.All.SendAsync("SeatAssigned", new
            {
                SeatId = assignmentInfo.SeatId,
                FlightNumber = assignmentInfo.FlightNumber,
                PassengerName = assignmentInfo.PassengerName,
                SeatNumber = assignmentInfo.SeatNumber,
                StaffName = assignmentInfo.StaffName,
                Timestamp = assignmentInfo.CheckInTime
            });

            // Also broadcast to flight-specific group
            await _hubContext.Clients.Group($"Flight_{assignmentInfo.FlightNumber}")
                .SendAsync("SeatAssigned", new
                {
                    SeatId = assignmentInfo.SeatId,
                    FlightNumber = assignmentInfo.FlightNumber,
                    PassengerName = assignmentInfo.PassengerName,
                    SeatNumber = assignmentInfo.SeatNumber,
                    StaffName = assignmentInfo.StaffName,
                    Timestamp = assignmentInfo.CheckInTime
                });

            _logger.LogInformation("🎉 Seat assignment completed successfully: {SeatNumber} → {PassengerName} on {FlightNumber}",
                assignmentInfo.SeatNumber, assignmentInfo.PassengerName, assignmentInfo.FlightNumber);

            return Ok(new
            {
                message = "Seat assigned successfully",
                seatNumber = assignmentInfo.SeatNumber,
                passengerName = assignmentInfo.PassengerName,
                flightNumber = assignmentInfo.FlightNumber,
                checkInTime = assignmentInfo.CheckInTime,
                staffName = assignmentInfo.StaffName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Unexpected error during seat assignment: Seat {SeatId} to Booking {BookingReference}",
                request.SeatId, request.BookingReference);

            return StatusCode(500, new
            {
                message = "Internal server error during seat assignment",
                timestamp = DateTime.UtcNow
            });
        }
    }

    // POST: api/checkin/test-race-condition
    [HttpPost("test-race-condition")]
    public async Task<ActionResult> TestRaceCondition([FromBody] RaceConditionTestRequest request)
    {
        _logger.LogInformation("🧪 Starting race condition test with {ClientCount} clients for seat {SeatId}",
            request.ClientCount, request.SeatId);

        var tasks = new List<Task<AssignSeatResponse>>();
        var clientId = 0;

        // Simulate multiple clients trying to assign the same seat
        for (int i = 0; i < request.ClientCount; i++)
        {
            var currentClientId = ++clientId;
            var fakeBookingRef = $"TEST{currentClientId:D3}";

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var assignRequest = new AssignSeatRequest
                    {
                        SeatId = request.SeatId,
                        BookingReference = fakeBookingRef,
                        StaffName = $"TestStaff{currentClientId}"
                    };

                    // Add small random delay to increase race condition likelihood
                    await Task.Delay(Random.Shared.Next(0, 100));

                    var result = await _concurrencyService.AssignSeatWithConcurrencyCheckAsync(
                        assignRequest.SeatId,
                        assignRequest.BookingReference,
                        assignRequest.StaffName);

                    return new AssignSeatResponse
                    {
                        ClientId = currentClientId,
                        Success = result.IsSuccess,
                        Message = result.IsSuccess ? "Assignment successful" : result.ErrorMessage,
                        ErrorType = result.IsSuccess ? null : result.ErrorType.ToString(),
                        Timestamp = DateTime.UtcNow
                    };
                }
                catch (Exception ex)
                {
                    return new AssignSeatResponse
                    {
                        ClientId = currentClientId,
                        Success = false,
                        Message = $"Exception: {ex.Message}",
                        ErrorType = "Exception",
                        Timestamp = DateTime.UtcNow
                    };
                }
            }));
        }

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r.Success);
        var conflictCount = results.Count(r => r.ErrorType == "Conflict");

        _logger.LogInformation("🧪 Race condition test completed: {SuccessCount} success, {ConflictCount} conflicts out of {TotalCount} attempts",
            successCount, conflictCount, request.ClientCount);

        return Ok(new
        {
            testResults = results.OrderBy(r => r.ClientId),
            summary = new
            {
                totalAttempts = request.ClientCount,
                successful = successCount,
                conflicts = conflictCount,
                errors = results.Count(r => !r.Success && r.ErrorType != "Conflict"),
                raceConditionHandled = conflictCount > 0 // If we have conflicts, race condition was detected and handled
            }
        });
    }

    // Models for requests and responses
    public class SearchRequest
    {
        public string PassportNumber { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
    }

    public class AssignSeatRequest
    {
        public string BookingReference { get; set; } = string.Empty;
        public string SeatId { get; set; } = string.Empty;
        public string? StaffName { get; set; }
    }

    public class RaceConditionTestRequest
    {
        public string SeatId { get; set; } = string.Empty;
        public int ClientCount { get; set; } = 5;
    }

    public class AssignSeatResponse
    {
        public int ClientId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorType { get; set; }
        public DateTime Timestamp { get; set; }
    }
}