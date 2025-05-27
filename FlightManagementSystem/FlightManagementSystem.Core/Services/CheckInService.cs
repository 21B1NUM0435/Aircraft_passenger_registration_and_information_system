using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Services
{
    public class CheckInService : ICheckInService
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IFlightRepository _flightRepository;
        private readonly ISeatRepository _seatRepository;
        private readonly ISeatAssignmentRepository _seatAssignmentRepository;
        private readonly IBoardingPassRepository _boardingPassRepository;
        private readonly ICheckInRecordRepository _checkInRecordRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISocketServer _socketServer;

        // Thread-safe dictionary to track seat reservation attempts
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _seatLocks = new();

        // Dictionary to track seats being processed to prevent race conditions
        private static readonly ConcurrentDictionary<string, DateTime> _seatProcessingTracker = new();

        public CheckInService(
            IBookingRepository bookingRepository,
            IFlightRepository flightRepository,
            ISeatRepository seatRepository,
            ISeatAssignmentRepository seatAssignmentRepository,
            IBoardingPassRepository boardingPassRepository,
            ICheckInRecordRepository checkInRecordRepository,
            IUnitOfWork unitOfWork,
            ISocketServer socketServer)
        {
            _bookingRepository = bookingRepository;
            _flightRepository = flightRepository;
            _seatRepository = seatRepository;
            _seatAssignmentRepository = seatAssignmentRepository;
            _boardingPassRepository = boardingPassRepository;
            _checkInRecordRepository = checkInRecordRepository;
            _unitOfWork = unitOfWork;
            _socketServer = socketServer;
        }

        public async Task<Booking?> FindBookingByPassportAsync(string passportNumber, string flightNumber)
        {
            return await _bookingRepository.GetBookingByPassportAndFlightAsync(passportNumber, flightNumber);
        }

        public async Task<IEnumerable<Seat>> GetAvailableSeatsAsync(string flightNumber)
        {
            var availableSeats = await _seatRepository.GetAvailableSeatsByFlightAsync(flightNumber);

            // Filter out seats that are currently being processed
            var filteredSeats = availableSeats.Where(seat =>
                !_seatProcessingTracker.ContainsKey(seat.SeatId) ||
                (DateTime.UtcNow - _seatProcessingTracker[seat.SeatId]).TotalSeconds > 30 // Timeout after 30 seconds
            ).ToList();

            // Clean up expired processing entries
            CleanupExpiredProcessingEntries();

            return filteredSeats;
        }

        public async Task<bool> IsSeatAvailableAsync(string seatId, string flightNumber)
        {
            // Check if seat is currently being processed
            if (_seatProcessingTracker.ContainsKey(seatId))
            {
                var processingTime = _seatProcessingTracker[seatId];
                if ((DateTime.UtcNow - processingTime).TotalSeconds < 30) // 30 second timeout
                {
                    return false; // Seat is being processed by another request
                }
                else
                {
                    // Remove expired entry
                    _seatProcessingTracker.TryRemove(seatId, out _);
                }
            }

            return await _seatAssignmentRepository.IsSeatAvailableAsync(seatId, flightNumber);
        }

        public async Task<(bool Success, string Message, BoardingPass? BoardingPass)> CheckInPassengerAsync(
    string bookingReference,
    string seatId,
    string staffId,
    string counterId)
        {
            // Get or create a semaphore for this specific seat
            var seatSemaphore = _seatLocks.GetOrAdd(seatId, _ => new SemaphoreSlim(1, 1));

            // Wait for exclusive access to this seat (with timeout)
            if (!await seatSemaphore.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                return (false, "Seat is currently being processed by another staff member. Please try again.", null);
            }

            try
            {
                // Mark seat as being processed
                _seatProcessingTracker[seatId] = DateTime.UtcNow;

                // Broadcast seat lock to other terminals immediately
                BroadcastSeatLock(seatId, true);

                // Get booking details
                var booking = await _bookingRepository.GetByReferenceWithDetailsAsync(bookingReference);
                if (booking == null)
                    return (false, "Booking not found", null);

                // Verify booking is not already checked in
                if (booking.CheckInStatus == CheckInStatus.CheckedIn)
                    return (false, "Passenger already checked in", null);

                // Check if flight exists and is in check-in status
                var flight = await _flightRepository.GetByFlightNumberWithDetailsAsync(booking.FlightNumber);
                if (flight == null)
                    return (false, "Flight not found", null);

                if (flight.Status != FlightStatus.CheckingIn)
                    return (false, $"Cannot check in - flight status is {flight.Status}", null);

                // Double-check seat availability (race condition protection)
                if (!await _seatAssignmentRepository.IsSeatAvailableAsync(seatId, flight.FlightNumber))
                {
                    return (false, "Seat is no longer available - it may have been assigned to another passenger", null);
                }

                try
                {
                    // Create seat assignment
                    var seatAssignment = new SeatAssignment
                    {
                        AssignmentId = Guid.NewGuid().ToString(),
                        BookingReference = bookingReference,
                        SeatId = seatId,
                        AssignedAt = DateTime.UtcNow
                    };
                    await _seatAssignmentRepository.AddAsync(seatAssignment);

                    // Create boarding pass
                    var boardingPass = new BoardingPass
                    {
                        BoardingPassId = Guid.NewGuid().ToString(),
                        BookingReference = bookingReference,
                        Gate = flight.Status == FlightStatus.Boarding ? "A12" : "TBD",
                        BoardingTime = flight.DepartureTime.AddMinutes(-30),
                        Barcode = $"BP-{bookingReference}-{DateTime.UtcNow.Ticks}",
                        IssuedAt = DateTime.UtcNow
                    };
                    await _boardingPassRepository.AddAsync(boardingPass);

                    // Create check-in record
                    var checkInRecord = new CheckInRecord
                    {
                        CheckInId = Guid.NewGuid().ToString(),
                        BookingReference = bookingReference,
                        CounterId = counterId,
                        StaffId = staffId,
                        CheckInTime = DateTime.UtcNow,
                        CheckInMethod = CheckInMethod.Counter
                    };
                    await _checkInRecordRepository.AddAsync(checkInRecord);

                    // Update booking status  
                    booking.CheckInStatus = CheckInStatus.CheckedIn;
                    await _bookingRepository.UpdateAsync(booking);

                    // Save all changes
                    await _unitOfWork.SaveChangesAsync();

                    // Broadcast successful seat assignment to all terminals
                    BroadcastSeatAssignment(seatId, flight.FlightNumber, booking.Passenger.FirstName + " " + booking.Passenger.LastName);

                    return (true, "Check-in successful", boardingPass);
                }
                catch (Exception ex)
                {
                    return (false, $"Check-in failed: {ex.Message}", null);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Check-in failed: {ex.Message}", null);
            }
            finally
            {
                // Remove seat from processing tracker
                _seatProcessingTracker.TryRemove(seatId, out _);

                // Broadcast seat unlock
                BroadcastSeatLock(seatId, false);

                // Release the semaphore
                seatSemaphore.Release();
            }
        }

        private void BroadcastSeatLock(string seatId, bool isLocked)
        {
            try
            {
                var message = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "SeatLock",
                    data = new
                    {
                        seatId = seatId,
                        isLocked = isLocked,
                        timestamp = DateTime.UtcNow
                    },
                    timestamp = DateTime.UtcNow
                });

                _socketServer.BroadcastMessage(message);
                Console.WriteLine($"Broadcasted seat lock: {seatId} = {isLocked}");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the check-in process
                Console.WriteLine($"Error broadcasting seat lock: {ex.Message}");
            }
        }

        private void BroadcastSeatAssignment(string seatId, string flightNumber, string passengerName)
        {
            try
            {
                var message = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "SeatAssignment",
                    data = new
                    {
                        seatId = seatId,
                        flightNumber = flightNumber,
                        passengerName = passengerName,
                        isAssigned = true,
                        timestamp = DateTime.UtcNow
                    },
                    timestamp = DateTime.UtcNow
                });

                _socketServer.BroadcastMessage(message);
                Console.WriteLine($"Broadcasted seat assignment: {seatId} on flight {flightNumber}");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the check-in process
                Console.WriteLine($"Error broadcasting seat assignment: {ex.Message}");
            }
        }

        private void CleanupExpiredProcessingEntries()
        {
            var expiredEntries = _seatProcessingTracker
                .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds > 30)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var expiredKey in expiredEntries)
            {
                _seatProcessingTracker.TryRemove(expiredKey, out _);
            }
        }

        // Static method to get current seat processing status (for debugging)
        public static Dictionary<string, DateTime> GetCurrentProcessingSeats()
        {
            return new Dictionary<string, DateTime>(_seatProcessingTracker);
        }

        // Method to manually clear a stuck seat (admin function)
        public static bool ClearSeatProcessing(string seatId)
        {
            return _seatProcessingTracker.TryRemove(seatId, out _);
        }
    }
}