using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface ISeatRepository : IRepository<Seat>
    {
        Task<IEnumerable<Seat>> GetSeatsByAircraftAsync(string aircraftId);
        Task<IEnumerable<Seat>> GetAvailableSeatsByFlightAsync(string flightNumber);
    }
}
