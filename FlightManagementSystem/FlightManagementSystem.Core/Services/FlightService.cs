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

        public FlightService(
            IFlightRepository flightRepository,
            IBookingRepository bookingRepository,
            IPassengerRepository passengerRepository,
            IUnitOfWork unitOfWork)
        {
            _flightRepository = flightRepository;
            _bookingRepository = bookingRepository;
            _passengerRepository = passengerRepository;
            _unitOfWork = unitOfWork;
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
            var result = await _flightRepository.UpdateFlightStatusAsync(flightNumber, newStatus);
            if (result)
            {
                await _unitOfWork.SaveChangesAsync();
            }
            return result;
        }

        public async Task<IEnumerable<Passenger>> GetCheckedInPassengersAsync(string flightNumber)
        {
            return await _passengerRepository.GetPassengersByFlightAsync(flightNumber);
        }
    }
}
