using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IFlightRepository : IRepository<Flight>
    {
        Task<Flight?> GetByFlightNumberWithDetailsAsync(string flightNumber);
        Task<IEnumerable<Flight>> GetFlightsByStatusAsync(FlightStatus status);
        Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus);
    }
}
