using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IFlightService
    {
        Task<IEnumerable<Flight>> GetAllFlightsAsync();
        Task<Flight?> GetFlightByNumberAsync(string flightNumber);
        Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus);
        Task<IEnumerable<Passenger>> GetCheckedInPassengersAsync(string flightNumber);
    }
}
