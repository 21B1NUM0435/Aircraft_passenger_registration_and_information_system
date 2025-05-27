using System.Collections.Concurrent;
using System.Data;
using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FlightSystem.Server.Services;

public interface IConcurrencyService
{
    Task<SeatAssignmentResult> AssignSeatWithConcurrencyCheckAsync(string seatId, string bookingReference, string staffName);
    Task<bool> TryLockSeatAsync(string seatId, string clientId, TimeSpan? timeout = null);
    Task ReleaseSeatLockAsync(string seatId, string clientId);
    Task<Dictionary<string, string>> GetActiveSeatLocksAsync();
    Task CleanupExpiredLocksAsync();
}

public class ConcurrencyService : IConcurrencyService
{
    private readonly FlightDbContext _context;
    private readonly ILogger<ConcurrencyService> _logger;
    private readonly IWebSocketService _webSocketService;

    // In-memory seat locks with expiration
    private readonly ConcurrentDictionary<string, SeatLockInfo> _seatLocks = new();

    // Single semaphore per seat to prevent deadlocks
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _seatSemaphores = new();

    // Cleanup timer
    private readonly Timer _cleanupTimer;

    // Configuration
    private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(30);

    public ConcurrencyService(
        FlightDbContext context,
        ILogger<ConcurrencyService> logger,
        IWebSocketService webSocketService)
    {
        _context = context;
        _logger = logger;
        _webSocketService = webSocketService;

        // Start cleanup timer - runs every minute
        _cleanupTimer = new Timer(
            async _ => await CleanupExpiredLocksAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public async Task<SeatAssignmentResult> AssignSeatWithConcurrencyCheckAsync(string seatId, string bookingReference, string staffName)
    {
        var operationId = Guid.NewGuid().ToString();
        _logger.LogInformation("🎯 Starting seat assignment operation {OperationId}: Seat {SeatId} to Booking {BookingReference}",
            operationId, seatId, bookingReference);

        // Get or create semaphore for this seat
        var semaphore = _seatSemaphores.GetOrAdd(seatId, _ => new SemaphoreSlim(1, 1));

        // Try to acquire semaphore with timeout
        var acquired = await semaphore.WaitAsync(_operationTimeout);
        if (!acquired)
        {
            _logger.LogWarning("⏰ Timeout acquiring semaphore for seat {SeatId} in operation {OperationId}", seatId, operationId);
            return SeatAssignmentResult.Error("Operation timed out - seat may be locked by another user");
        }

        try
        {
            return await PerformSeatAssignmentAsync(seatId, bookingReference, staffName, operationId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<SeatAssignmentResult> PerformSeatAssignmentAsync(string seatId, string bookingReference, string staffName, string operationId)
    {
        // Use Serializable isolation level to prevent phantom reads
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            _logger.LogDebug("🔒 Acquired database transaction with Serializable isolation for operation {OperationId}", operationId);

            // Step 1: Check if seat is locked by another client
            if (_seatLocks.TryGetValue(seatId, out var lockInfo) && !lockInfo.IsExpired())
            {
                return SeatAssignmentResult.Conflict($"Seat is currently locked by another user. Please try again in a few moments.");
            }

            // Step 2: Get seat with database-level locking (SELECT FOR UPDATE equivalent in SQLite)
            var seat = await _context.Seats
                .Where(s => s.SeatId == seatId)
                .FirstOrDefaultAsync();

            if (seat == null)
            {
                return SeatAssignmentResult.NotFound($"Seat {seatId} not found");
            }

            // Step 3: Double-check seat availability with fresh data
            if (!seat.IsAvailable)
            {
                // Get current booking info
                var existingBooking = await _context.Bookings
                    .Include(b => b.Passenger)
                    .Where(b => b.SeatId == seatId)
                    .FirstOrDefaultAsync();

                var occupiedBy = existingBooking?.Passenger?.FullName ?? "Unknown passenger";
                return SeatAssignmentResult.Conflict($"Seat {seat.SeatNumber} is already assigned to {occupiedBy}");
            }

            // Step 4: Verify booking exists and is valid
            var booking = await _context.Bookings
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Where(b => b.BookingReference == bookingReference)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return SeatAssignmentResult.NotFound($"Booking {bookingReference} not found");
            }

            // Step 5: Validate business rules
            var validationResult = ValidateBookingForSeatAssignment(booking, seat, operationId);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // Step 6: Perform the assignment atomically
            await AssignSeatToBookingAsync(seat, booking, staffName, operationId);

            // Step 7: Commit transaction
            await transaction.CommitAsync();

            _logger.LogInformation("✅ Seat assignment completed successfully in operation {OperationId}: {SeatNumber} → {PassengerName}",
                operationId, seat.SeatNumber, booking.Passenger.FullName);

            // Step 8: Notify other clients via WebSocket
            await _webSocketService.BroadcastSeatLockAsync(seatId, booking.FlightNumber, true, staffName);

            return SeatAssignmentResult.Success(new SeatAssignmentInfo
            {
                SeatId = seatId,
                SeatNumber = seat.SeatNumber,
                FlightNumber = booking.FlightNumber,
                PassengerName = booking.Passenger.FullName,
                CheckInTime = booking.CheckInTime!.Value,
                StaffName = staffName
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning("🔄 Concurrency conflict detected in operation {OperationId}: {Message}", operationId, ex.Message);
            return SeatAssignmentResult.Conflict("Another user modified this seat at the same time. Please refresh and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning("🚫 Unique constraint violation in operation {OperationId}: {Message}", operationId, ex.Message);
            return SeatAssignmentResult.Conflict("This seat has been assigned to another passenger. Please select a different seat.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Unexpected error in seat assignment operation {OperationId}", operationId);
            return SeatAssignmentResult.Error($"An unexpected error occurred during seat assignment: {ex.Message}");
        }
    }

    private SeatAssignmentResult ValidateBookingForSeatAssignment(Booking booking, Seat seat, string operationId)
    {
        // Check if passenger already checked in
        if (booking.Status == BookingStatus.CheckedIn)
        {
            return SeatAssignmentResult.Conflict($"Passenger {booking.Passenger.FullName} is already checked in");
        }

        // Check if seat belongs to the same flight
        if (seat.FlightNumber != booking.FlightNumber)
        {
            return SeatAssignmentResult.Conflict($"Seat {seat.SeatNumber} belongs to flight {seat.FlightNumber}, but booking is for flight {booking.FlightNumber}");
        }

        // Check flight status
        if (booking.Flight.Status == FlightStatus.Departed)
        {
            return SeatAssignmentResult.Conflict($"Cannot check in - flight {booking.FlightNumber} has already departed");
        }

        if (booking.Flight.Status == FlightStatus.Cancelled)
        {
            return SeatAssignmentResult.Conflict($"Cannot check in - flight {booking.FlightNumber} has been cancelled");
        }

        return SeatAssignmentResult.Success(null!); // Validation passed
    }

    private async Task AssignSeatToBookingAsync(Seat seat, Booking booking, string staffName, string operationId)
    {
        // Update seat availability
        seat.IsAvailable = false;

        // Update booking with seat assignment and check-in info
        booking.SeatId = seat.SeatId;
        booking.Status = BookingStatus.CheckedIn;
        booking.CheckInTime = DateTime.UtcNow;
        booking.CheckInStaff = staffName;

        // Save changes
        var changesCount = await _context.SaveChangesAsync();

        _logger.LogDebug("💾 Saved {ChangesCount} changes to database in operation {OperationId}", changesCount, operationId);
    }

    public async Task<bool> TryLockSeatAsync(string seatId, string clientId, TimeSpan? timeout = null)
    {
        var lockTimeout = timeout ?? _lockTimeout;
        var lockInfo = new SeatLockInfo
        {
            SeatId = seatId,
            ClientId = clientId,
            LockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lockTimeout)
        };

        var lockAcquired = _seatLocks.TryAdd(seatId, lockInfo);

        if (lockAcquired)
        {
            _logger.LogInformation("🔒 Seat lock acquired: {SeatId} by client {ClientId} until {ExpiresAt}",
                seatId, clientId, lockInfo.ExpiresAt);

            // Get flight number for broadcasting
            var seat = await _context.Seats.FirstOrDefaultAsync(s => s.SeatId == seatId);
            if (seat != null)
            {
                await _webSocketService.BroadcastSeatLockAsync(seatId, seat.FlightNumber, true, clientId);
            }

            return true;
        }
        else
        {
            // Check if existing lock is expired
            if (_seatLocks.TryGetValue(seatId, out var existingLock) && existingLock.IsExpired())
            {
                // Try to replace expired lock
                if (_seatLocks.TryUpdate(seatId, lockInfo, existingLock))
                {
                    _logger.LogInformation("🔄 Replaced expired seat lock: {SeatId} by client {ClientId}", seatId, clientId);

                    var seat = await _context.Seats.FirstOrDefaultAsync(s => s.SeatId == seatId);
                    if (seat != null)
                    {
                        await _webSocketService.BroadcastSeatLockAsync(seatId, seat.FlightNumber, true, clientId);
                    }

                    return true;
                }
            }

            _logger.LogWarning("🚫 Seat lock denied: {SeatId} for client {ClientId} (already locked by {ExistingClient})",
                seatId, clientId, existingLock?.ClientId ?? "unknown");

            return false;
        }
    }

    public async Task ReleaseSeatLockAsync(string seatId, string clientId)
    {
        if (_seatLocks.TryGetValue(seatId, out var lockInfo) && lockInfo.ClientId == clientId)
        {
            if (_seatLocks.TryRemove(seatId, out var removedLock))
            {
                _logger.LogInformation("🔓 Seat lock released: {SeatId} by client {ClientId}", seatId, clientId);

                // Notify other clients
                var seat = await _context.Seats.FirstOrDefaultAsync(s => s.SeatId == seatId);
                if (seat != null)
                {
                    await _webSocketService.BroadcastSeatLockAsync(seatId, seat.FlightNumber, false, clientId);
                }
            }
        }
        else
        {
            _logger.LogWarning("⚠️ Attempted to release seat lock not owned by client: {SeatId} by {ClientId}", seatId, clientId);
        }
    }

    public async Task<Dictionary<string, string>> GetActiveSeatLocksAsync()
    {
        await CleanupExpiredLocksAsync();

        return _seatLocks
            .Where(kvp => !kvp.Value.IsExpired())
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ClientId);
    }

    public async Task CleanupExpiredLocksAsync()
    {
        var expiredLocks = _seatLocks
            .Where(kvp => kvp.Value.IsExpired())
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var seatId in expiredLocks)
        {
            if (_seatLocks.TryRemove(seatId, out var expiredLock))
            {
                _logger.LogInformation("🧹 Cleaned up expired seat lock: {SeatId} (expired at {ExpiresAt})",
                    seatId, expiredLock.ExpiresAt);

                // Notify clients that seat is now available
                var seat = await _context.Seats.FirstOrDefaultAsync(s => s.SeatId == seatId);
                if (seat != null)
                {
                    await _webSocketService.BroadcastSeatLockAsync(seatId, seat.FlightNumber, false, "system");
                }
            }
        }

        if (expiredLocks.Count > 0)
        {
            _logger.LogInformation("🧹 Cleaned up {Count} expired seat locks", expiredLocks.Count);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();

        // Dispose all semaphores
        foreach (var semaphore in _seatSemaphores.Values)
        {
            semaphore.Dispose();
        }

        _seatSemaphores.Clear();
        _seatLocks.Clear();
    }
}

// Supporting classes
public class SeatLockInfo
{
    public string SeatId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime LockedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
}

public class SeatAssignmentResult
{
    public bool IsSuccess { get; set; }
    public SeatAssignmentInfo? Data { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public SeatAssignmentErrorType ErrorType { get; set; }

    public static SeatAssignmentResult Success(SeatAssignmentInfo data) => new() { IsSuccess = true, Data = data };
    public static SeatAssignmentResult NotFound(string message) => new() { IsSuccess = false, ErrorMessage = message, ErrorType = SeatAssignmentErrorType.NotFound };
    public static SeatAssignmentResult Conflict(string message) => new() { IsSuccess = false, ErrorMessage = message, ErrorType = SeatAssignmentErrorType.Conflict };
    public static SeatAssignmentResult Error(string message) => new() { IsSuccess = false, ErrorMessage = message, ErrorType = SeatAssignmentErrorType.Error };
}

public class SeatAssignmentInfo
{
    public string SeatId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; }
    public string StaffName { get; set; } = string.Empty;
}

public enum SeatAssignmentErrorType
{
    NotFound,
    Conflict,
    Error
}