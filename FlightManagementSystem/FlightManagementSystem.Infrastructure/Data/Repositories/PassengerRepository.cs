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
    public class PassengerRepository : GenericRepository<Passenger>, IPassengerRepository
    {
        public PassengerRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Passenger?> GetByPassportNumberAsync(string passportNumber)
        {
            return await _context.Passengers
                .Include(p => p.Bookings)
                .FirstOrDefaultAsync(p => p.PassportNumber == passportNumber);
        }

        public async Task<IEnumerable<Passenger>> GetPassengersByFlightAsync(string flightNumber)
        {
            return await _context.Passengers
                .Where(p => p.Bookings.Any(b => b.FlightNumber == flightNumber && b.CheckInStatus == CheckInStatus.CheckedIn))
                .ToListAsync();
        }
    }
}