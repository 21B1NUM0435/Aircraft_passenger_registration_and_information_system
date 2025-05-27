using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface ISeatAssignmentRepository : IRepository<SeatAssignment>
    {
        Task<SeatAssignment?> GetBySeatAndFlightAsync(string seatId, string flightNumber);
        Task<bool> IsSeatAvailableAsync(string seatId, string flightNumber);
    }
}
