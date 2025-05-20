using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagementSystem.Infrastructure.Data.Repositories
{
    public class CheckInRecordRepository : GenericRepository<CheckInRecord>, ICheckInRecordRepository
    {
        public CheckInRecordRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<CheckInRecord>> GetByBookingReferenceAsync(string bookingReference)
        {
            return await _context.CheckInRecords
                .Where(r => r.BookingReference == bookingReference)
                .ToListAsync();
        }

        public async Task<IEnumerable<CheckInRecord>> GetByStaffIdAsync(string staffId)
        {
            return await _context.CheckInRecords
                .Where(r => r.StaffId == staffId)
                .ToListAsync();
        }
    }
}
