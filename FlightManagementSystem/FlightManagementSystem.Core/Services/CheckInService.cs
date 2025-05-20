using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public CheckInService(
            IBookingRepository bookingRepository,
            IFlightRepository flightRepository,
            ISeatRepository seatRepository,
            ISeatAssignmentRepository seatAssignmentRepository,
            IBoardingPassRepository boardingPassRepository,
            ICheckInRecordRepository checkInRecordRepository,
            IUnitOfWork unitOfWork)
        {
            _bookingRepository = bookingRepository;
            _flightRepository = flightRepository;
            _seatRepository = seatRepository;
            _seatAssignmentRepository = seatAssignmentRepository;
            _boardingPassRepository = boardingPassRepository;
            _checkInRecordRepository = checkInRecordRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Booking?> FindBookingByPassportAsync(string passportNumber, string flightNumber)
        {
            return await _bookingRepository.GetBookingByPassportAndFlightAsync(passportNumber, flightNumber);
        }

        public async Task<IEnumerable<Seat>> GetAvailableSeatsAsync(string flightNumber)
        {
            return await _seatRepository.GetAvailableSeatsByFlightAsync(flightNumber);
        }

        public async Task<bool> IsSeatAvailableAsync(string seatId, string flightNumber)
        {
            return await _seatAssignmentRepository.IsSeatAvailableAsync(seatId, flightNumber);
        }

        public async Task<(bool Success, string Message, BoardingPass? BoardingPass)> CheckInPassengerAsync(
            string bookingReference,
            string seatId,
            string staffId,
            string counterId)
        {
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

            // Check if seat is available - this needs to be atomic
            if (!await _seatAssignmentRepository.IsSeatAvailableAsync(seatId, flight.FlightNumber))
                return (false, "Seat is no longer available", null);

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
                    Gate = flight.Status == FlightStatus.Boarding ? "TBD" : "TBD", // Gate would be assigned later if not boarding
                    BoardingTime = flight.DepartureTime.AddMinutes(-30), // 30 minutes before departure
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

                // Save all changes in a transaction
                await _unitOfWork.SaveChangesAsync();

                return (true, "Check-in successful", boardingPass);
            }
            catch (Exception ex)
            {
                // Log error here
                return (false, $"Check-in failed: {ex.Message}", null);
            }
        }
    }
}