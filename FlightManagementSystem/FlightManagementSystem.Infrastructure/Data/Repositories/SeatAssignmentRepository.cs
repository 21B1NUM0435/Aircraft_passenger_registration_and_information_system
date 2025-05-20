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
    public class SeatAssignmentRepository : GenericRepository<SeatAssignment>, ISeatAssignmentRepository
    {
        public SeatAssignmentRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<SeatAssignment?> GetBySeatAndFlightAsync(string seatId, string flightNumber)
        {
            return await _context.SeatAssignments
                .Include(sa => sa.Booking)
                .FirstOrDefaultAsync(sa => sa.SeatId == seatId && sa.Booking.FlightNumber == flightNumber);
        }

        public async Task<bool> IsSeatAvailableAsync(string seatId, string flightNumber)
        {
            // Check if there's any seat assignment for this seat on this flight
            return !await _context.SeatAssignments
                .AnyAsync(sa => sa.SeatId == seatId && sa.Booking.FlightNumber == flightNumber);
        }
    }
}
