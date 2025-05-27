using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IPassengerRepository : IRepository<Passenger>
    {
        Task<Passenger?> GetByPassportNumberAsync(string passportNumber);
        Task<IEnumerable<Passenger>> GetPassengersByFlightAsync(string flightNumber);
    }
}
