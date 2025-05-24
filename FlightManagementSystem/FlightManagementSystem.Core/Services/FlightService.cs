using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Services
{
    public class FlightService : IFlightService
    {
        private readonly IFlightRepository _flightRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly IPassengerRepository _passengerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISocketServer _socketServer;
        private readonly IFlightHubService? _flightHubService;

        public FlightService(
            IFlightRepository flightRepository,
            IBookingRepository bookingRepository,
            IPassengerRepository passengerRepository,
            IUnitOfWork unitOfWork,
            ISocketServer socketServer,
            IFlightHubService? flightHubService = null)
        {
            _flightRepository = flightRepository;
            _bookingRepository = bookingRepository;
            _passengerRepository = passengerRepository;
            _unitOfWork = unitOfWork;
            _socketServer = socketServer;
            _flightHubService = flightHubService;
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
                // Update flight status in database
                var result = await _flightRepository.UpdateFlightStatusAsync(flightNumber, newStatus);

                if (result)
                {
                    await _unitOfWork.SaveChangesAsync();

                    // Broadcast to Windows applications via Socket Server
                    await BroadcastFlightStatusToSocketClients(flightNumber, newStatus);

                    // Broadcast to Web clients via SignalR Hub
                    if (_flightHubService != null)
                    {
                        await _flightHubService.NotifyFlightStatusChanged(flightNumber, newStatus);
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Log error but don't expose internal details
                Console.WriteLine($"Error updating flight status: {ex.Message}");
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

        private async Task BroadcastFlightStatusToSocketClients(string flightNumber, FlightStatus newStatus)
        {
            try
            {
                var message = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "FlightStatusUpdate",
                    data = new
                    {
                        flightNumber = flightNumber,
                        newStatus = newStatus.ToString(),
                        timestamp = DateTime.UtcNow
                    }
                });

                _socketServer.BroadcastMessage(message);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Error broadcasting flight status to socket clients: {ex.Message}");
            }
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