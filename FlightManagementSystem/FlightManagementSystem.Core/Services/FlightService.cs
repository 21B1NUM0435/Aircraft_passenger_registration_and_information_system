using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace FlightManagementSystem.Core.Services
{
    public class FlightService : IFlightService
    {
        private readonly IFlightRepository _flightRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly IPassengerRepository _passengerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FlightService>? _logger;

        public FlightService(
            IFlightRepository flightRepository,
            IBookingRepository bookingRepository,
            IPassengerRepository passengerRepository,
            IUnitOfWork unitOfWork,
            ILogger<FlightService>? logger = null)
        {
            _flightRepository = flightRepository;
            _bookingRepository = bookingRepository;
            _passengerRepository = passengerRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<Flight>> GetAllFlightsAsync()
        {
            return await _flightRepository.GetAllAsync();
        }

        public async Task<Flight?> GetFlightByNumberAsync(string flightNumber)
        {
            return await _flightRepository.GetByFlightNumberWithDetailsAsync(flightNumber);
        }

        public async Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus)
        {
            try
            {
                _logger?.LogInformation("Updating flight {FlightNumber} status to {NewStatus}", flightNumber, newStatus);

                // Update flight status in database
                var result = await _flightRepository.UpdateFlightStatusAsync(flightNumber, newStatus);

                if (result)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger?.LogInformation("Successfully updated flight {FlightNumber} status to {NewStatus}", flightNumber, newStatus);
                    return true;
                }

                _logger?.LogWarning("Failed to update flight {FlightNumber} status", flightNumber);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating flight {FlightNumber} status to {NewStatus}", flightNumber, newStatus);
                return false;
            }
        }

        public async Task<IEnumerable<Passenger>> GetCheckedInPassengersAsync(string flightNumber)
        {
            return await _passengerRepository.GetPassengersByFlightAsync(flightNumber);
        }

        public async Task<FlightStatistics> GetFlightStatisticsAsync(string flightNumber)
        {
            var flight = await _flightRepository.GetByFlightNumberWithDetailsAsync(flightNumber);
            if (flight == null)
            {
                return new FlightStatistics();
            }

            var allBookings = await _bookingRepository.GetBookingsByFlightNumberAsync(flightNumber);
            var checkedInBookings = allBookings.Where(b => b.CheckInStatus == CheckInStatus.CheckedIn);

            return new FlightStatistics
            {
                FlightNumber = flightNumber,
                TotalBookings = allBookings.Count(),
                CheckedInPassengers = checkedInBookings.Count(),
                CheckInPercentage = allBookings.Any() ? (double)checkedInBookings.Count() / allBookings.Count() * 100 : 0,
                FlightStatus = flight.Status.ToString(),
                DepartureTime = flight.DepartureTime,
                AircraftCapacity = flight.Aircraft?.Capacity ?? 0
            };
        }
    }

    // Statistics model for flight information
    public class FlightStatistics
    {
        public string FlightNumber { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public int CheckedInPassengers { get; set; }
        public double CheckInPercentage { get; set; }
        public string FlightStatus { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public int AircraftCapacity { get; set; }
        public int AvailableSeats => AircraftCapacity - CheckedInPassengers;
    }
}