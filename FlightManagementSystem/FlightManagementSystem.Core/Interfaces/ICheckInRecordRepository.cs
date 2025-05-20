using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface ICheckInRecordRepository : IRepository<CheckInRecord>
    {
        Task<IEnumerable<CheckInRecord>> GetByBookingReferenceAsync(string bookingReference);
        Task<IEnumerable<CheckInRecord>> GetByStaffIdAsync(string staffId);
    }
}
