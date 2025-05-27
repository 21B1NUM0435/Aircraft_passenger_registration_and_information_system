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
    public class SeatRepository : GenericRepository<Seat>, ISeatRepository
    {
        public SeatRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Seat>> GetSeatsByAircraftAsync(string aircraftId)
        {
            return await _context.Seats
                .Where(s => s.AircraftId == aircraftId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Seat>> GetAvailableSeatsByFlightAsync(string flightNumber)
        {
            // Get the aircraft for this flight
            var flight = await _context.Flights
                .Include(f => f.Aircraft)
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);

            if (flight == null)
                return Enumerable.Empty<Seat>();

            // Get all seats for this aircraft
            var allSeats = await _context.Seats
                .Where(s => s.AircraftId == flight.AircraftId)
                .ToListAsync();

            // Get all seat assignments for this flight
            var assignedSeatIds = await _context.SeatAssignments
                .Where(sa => sa.Booking.FlightNumber == flightNumber)
                .Select(sa => sa.SeatId)
                .ToListAsync();

            // Return seats that don't have assignments
            return allSeats.Where(s => !assignedSeatIds.Contains(s.SeatId));
        }
    }
}
