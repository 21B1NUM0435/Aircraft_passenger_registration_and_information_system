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
    public class BoardingPassRepository : GenericRepository<BoardingPass>, IBoardingPassRepository
    {
        public BoardingPassRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<BoardingPass?> GetByBookingReferenceAsync(string bookingReference)
        {
            return await _context.BoardingPasses
                .Include(bp => bp.Booking)
                .ThenInclude(b => b.Passenger)
                .Include(bp => bp.Booking)
                .ThenInclude(b => b.Flight)
                .FirstOrDefaultAsync(bp => bp.BookingReference == bookingReference);
        }

        public async Task<bool> HasBoardingPassAsync(string bookingReference)
        {
            return await _context.BoardingPasses
                .AnyAsync(bp => bp.BookingReference == bookingReference);
        }
    }
}
