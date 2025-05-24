using System.Collections.Concurrent;
using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightSystem.Server.Services;

public interface IConcurrencyService
{
    Task<ConcurrencyResult<T>> ExecuteWithLockAsync<T>(string lockKey, Func<Task<T>> operation, TimeSpan? timeout = null);
    Task<bool> TryLockSeatAsync(string seatId, TimeSpan? timeout = null);
    Task ReleaseSeatLockAsync(string seatId);
    Task<SeatAssignmentResult> AssignSeatWithConcurrencyCheckAsync(string seatId, string bookingReference, string staffName);
}

public class ConcurrencyService : IConcurrencyService
{
    private readonly FlightDbContext _context;
    private readonly ILogger<ConcurrencyService> _logger;

    // Thread-safe locks for different operations
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _seatLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _flightLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _bookingLocks = new();

    // Track active operations
    private static readonly ConcurrentDictionary<string, OperationInfo> _activeOperations = new();

    public ConcurrencyService(FlightDbContext context, ILogger<ConcurrencyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ConcurrencyResult<T>> ExecuteWithLockAsync<T>(string lockKey, Func<Task<T>> operation, TimeSpan? timeout = null)
    {
        var semaphore = GetOrCreateSemaphore(lockKey);
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);
        var operationId = Guid.NewGuid().ToString();

        _logger.LogDebug("🔒 Attempting to acquire lock for key: {LockKey}, Operation: {OperationId}", lockKey, operationId);

        var acquired = await semaphore.WaitAsync(timeoutValue);
        if (!acquired)
        {
            _logger.LogWarning("⏰ Lock acquisition timeout for key: {LockKey}, Operation: {OperationId}", lockKey, operationId);
            return ConcurrencyResult<T>.Timeout($"Operation timed out waiting for lock: {lockKey}");
        }

        try
        {
            // Track the operation
            _activeOperations[operationId] = new OperationInfo
            {
                LockKey = lockKey,
                StartTime = DateTime.UtcNow,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            _logger.LogDebug("✅ Lock acquired for key: {LockKey}, Operation: {OperationId}", lockKey, operationId);

            var result = await operation();

            _logger.LogDebug("🎯 Operation completed successfully for key: {LockKey}, Operation: {OperationId}", lockKey, operationId);
            return ConcurrencyResult<T>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Operation failed for key: {LockKey}, Operation: {OperationId}", lockKey, operationId);
            return ConcurrencyResult<T>.Error(ex.Message);
        }
        finally
        {
            _activeOperations.TryRemove(operationId, out _);
            semaphore.Release();
            _logger.LogDebug("🔓 Lock released for key: {LockKey}, Operation: {OperationId}", lockKey, operationId);
        }
    }

    public async Task<bool> TryLockSeatAsync(string seatId, TimeSpan? timeout = null)
    {
        var semaphore = _seatLocks.GetOrAdd(seatId, _ => new SemaphoreSlim(1, 1));
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(10);

        _logger.LogDebug("🪑 Attempting to lock seat: {SeatId}", seatId);

        var acquired = await semaphore.WaitAsync(timeoutValue);
        if (!acquired)
        {
            _logger.LogWarning("🪑⏰ Seat lock timeout: {SeatId}", seatId);
        }
        else
        {
            _logger.LogDebug("🪑✅ Seat locked: {SeatId}", seatId);
        }

        return acquired;
    }

    public async Task ReleaseSeatLockAsync(string seatId)
    {
        if (_seatLocks.TryGetValue(seatId, out var semaphore))
        {
            semaphore.Release();
            _logger.LogDebug("🪑🔓 Seat lock released: {SeatId}", seatId);
        }
        await Task.CompletedTask;
    }

    public async Task<SeatAssignmentResult> AssignSeatWithConcurrencyCheckAsync(string seatId, string bookingReference, string staffName)
    {
        var lockKey = $"seat-assignment-{seatId}";

        var result = await ExecuteWithLockAsync(lockKey, async () =>
        {
            _logger.LogInformation("🎯 Starting seat assignment: Seat {SeatId} to Booking {BookingReference} by {StaffName}",
                seatId, bookingReference, staffName);

            // Use explicit transaction with isolation level
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                // Double-check seat availability with fresh data
                var seat = await _context.Seats
                    .Where(s => s.SeatId == seatId)
                    .FirstOrDefaultAsync();

                if (seat == null)
                {
                    return SeatAssignmentResult.NotFound($"Seat {seatId} not found");
                }

                if (!seat.IsAvailable)
                {
                    // Get current booking for this seat
                    var currentBooking = await _context.Bookings
                        .Include(b => b.Passenger)
                        .Where(b => b.SeatId == seatId)
                        .FirstOrDefaultAsync();

                    var occupiedBy = currentBooking?.Passenger?.FullName ?? "Unknown";
                    return SeatAssignmentResult.Conflict($"Seat {seat.SeatNumber} is already assigned to {occupiedBy}");
                }

                // Verify booking exists and is valid
                var booking = await _context.Bookings
                    .Include(b => b.Passenger)
                    .Include(b => b.Flight)
                    .Where(b => b.BookingReference == bookingReference)
                    .FirstOrDefaultAsync();

                if (booking == null)
                {
                    return SeatAssignmentResult.NotFound($"Booking {bookingReference} not found");
                }

                if (booking.Status == BookingStatus.CheckedIn)
                {
                    var currentSeat = await _context.Seats
                        .Where(s => s.SeatId == booking.SeatId)
                        .FirstOrDefaultAsync();

                    return SeatAssignmentResult.Conflict($"Passenger {booking.Passenger.FullName} is already checked in to seat {currentSeat?.SeatNumber ?? "Unknown"}");
                }

                // Verify seat belongs to the same flight
                if (seat.FlightNumber != booking.FlightNumber)
                {
                    return SeatAssignmentResult.Conflict($"Seat {seat.SeatNumber} belongs to flight {seat.FlightNumber}, but booking is for flight {booking.FlightNumber}");
                }

                // Check for any other pending assignments to this seat in the last few seconds
                var recentAttempts = _activeOperations.Values
                    .Where(op => op.LockKey.Contains(seatId) &&
                                op.StartTime > DateTime.UtcNow.AddSeconds(-5) &&
                                op.ThreadId != Thread.CurrentThread.ManagedThreadId)
                    .ToList();

                if (recentAttempts.Any())
                {
                    _logger.LogWarning("🚨 Concurrent seat assignment detected for seat {SeatId} - recent attempts: {Count}",
                        seatId, recentAttempts.Count);

                    // Small random delay to reduce thundering herd
                    await Task.Delay(Random.Shared.Next(100, 500));

                    // Re-check seat availability after delay
                    await _context.Entry(seat).ReloadAsync();
                    if (!seat.IsAvailable)
                    {
                        return SeatAssignmentResult.Conflict($"Seat {seat.SeatNumber} was assigned to another passenger during processing");
                    }
                }

                // Perform the assignment
                seat.IsAvailable = false;
                booking.SeatId = seatId;
                booking.Status = BookingStatus.CheckedIn;
                booking.CheckInTime = DateTime.UtcNow;
                booking.CheckInStaff = staffName;

                // Save changes
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("✅ Seat assignment successful: Seat {SeatNumber} assigned to {PassengerName} on flight {FlightNumber}",
                    seat.SeatNumber, booking.Passenger.FullName, booking.FlightNumber);

                return SeatAssignmentResult.Success(new SeatAssignmentInfo
                {
                    SeatId = seatId,
                    SeatNumber = seat.SeatNumber,
                    FlightNumber = booking.FlightNumber,
                    PassengerName = booking.Passenger.FullName,
                    CheckInTime = booking.CheckInTime.Value,
                    StaffName = staffName
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Seat assignment failed: Seat {SeatId} to Booking {BookingReference}", seatId, bookingReference);
                throw;
            }
        }, TimeSpan.FromSeconds(30));

        return result.IsSuccess ? result.Data! : SeatAssignmentResult.Error(result.ErrorMessage);
    }

    private SemaphoreSlim GetOrCreateSemaphore(string key)
    {
        return _seatLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    // Cleanup method (optional - for removing unused semaphores)
    public void CleanupUnusedLocks()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        var expiredOperations = _activeOperations
            .Where(kvp => kvp.Value.StartTime < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var operationId in expiredOperations)
        {
            _activeOperations.TryRemove(operationId, out _);
        }
    }
}

// Supporting classes
public class ConcurrencyResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsTimeout { get; set; }

    public static ConcurrencyResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static ConcurrencyResult<T> Error(string message) => new() { IsSuccess = false, ErrorMessage = message };
    public static ConcurrencyResult<T> Timeout(string message) => new() { IsSuccess = false, ErrorMessage = message, IsTimeout = true };
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

public class OperationInfo
{
    public string LockKey { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int ThreadId { get; set; }
}