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
    public class FlightRepository : GenericRepository<Flight>, IFlightRepository
    {
        public FlightRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Flight?> GetByFlightNumberWithDetailsAsync(string flightNumber)
        {
            return await _context.Flights
                .Include(f => f.Aircraft)
                .Include(f => f.Bookings)
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);
        }

        public async Task<IEnumerable<Flight>> GetFlightsByStatusAsync(FlightStatus status)
        {
            return await _context.Flights
                .Where(f => f.Status == status)
                .ToListAsync();
        }

        public async Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus)
        {
            var flight = await _context.Flights.FindAsync(flightNumber);
            if (flight == null)
                return false;

            flight.Status = newStatus;
            return true;
        }
    }
}