using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using FlightSystem.Server.Hubs;
using System.Collections.Concurrent;

namespace FlightSystem.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckInController : ControllerBase
{
    private readonly FlightDbContext _context;
    private readonly IHubContext<FlightHub> _hubContext;
    private readonly ILogger<CheckInController> _logger;

    // Thread-safe seat locking mechanism
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _seatLocks = new();

    public CheckInController(FlightDbContext context, IHubContext<FlightHub> hubContext, ILogger<CheckInController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    // POST: api/checkin/search
    [HttpPost("search")]
    public async Task<ActionResult> SearchPassenger([FromBody] SearchRequest request)
    {
        _logger.LogInformation("🔍 Searching for passenger: {PassportNumber} on flight {FlightNumber}",
            request.PassportNumber, request.FlightNumber);

        var booking = await _context.Bookings
            .Include(b => b.Passenger)
            .Include(b => b.Flight)
            .Include(b => b.Seat)
            .FirstOrDefaultAsync(b => b.PassportNumber == request.PassportNumber
                                   && b.FlightNumber == request.FlightNumber);

        if (booking == null)
        {
            return NotFound(new { message = "No booking found for this passport and flight" });
        }

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

    // POST: api/checkin/assign-seat
    [HttpPost("assign-seat")]
    public async Task<ActionResult> AssignSeat([FromBody] AssignSeatRequest request)
    {
        _logger.LogInformation("🪑 Assigning seat {SeatId} to booking {BookingReference}",
            request.SeatId, request.BookingReference);

        // Get seat-specific semaphore for thread safety
        var semaphore = _seatLocks.GetOrAdd(request.SeatId, _ => new SemaphoreSlim(1, 1));

        // Wait for exclusive access to this seat
        await semaphore.WaitAsync();
        try
        {
            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check if seat is still available
                var seat = await _context.Seats
                    .FirstOrDefaultAsync(s => s.SeatId == request.SeatId);

                if (seat == null)
                {
                    return NotFound(new { message = "Seat not found" });
                }

                if (!seat.IsAvailable)
                {
                    return BadRequest(new { message = "Seat is no longer available" });
                }

                // Check if booking exists
                var booking = await _context.Bookings
                    .Include(b => b.Passenger)
                    .Include(b => b.Flight)
                    .FirstOrDefaultAsync(b => b.BookingReference == request.BookingReference);

                if (booking == null)
                {
                    return NotFound(new { message = "Booking not found" });
                }

                if (booking.Status == BookingStatus.CheckedIn)
                {
                    return BadRequest(new { message = "Passenger already checked in" });
                }

                // Assign seat
                seat.IsAvailable = false;
                booking.SeatId = request.SeatId;
                booking.Status = BookingStatus.CheckedIn;
                booking.CheckInTime = DateTime.UtcNow;
                booking.CheckInStaff = request.StaffName ?? "Unknown";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Broadcast seat assignment to all clients
                await _hubContext.Clients.All.SendAsync("SeatAssigned", new
                {
                    SeatId = request.SeatId,
                    FlightNumber = booking.FlightNumber,
                    PassengerName = booking.Passenger.FullName,
                    SeatNumber = seat.SeatNumber,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("✅ Seat {SeatId} assigned to {PassengerName}",
                    request.SeatId, booking.Passenger.FullName);

                return Ok(new
                {
                    message = "Seat assigned successfully",
                    seatNumber = seat.SeatNumber,
                    passengerName = booking.Passenger.FullName,
                    checkInTime = booking.CheckInTime
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    // Models for requests
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
}