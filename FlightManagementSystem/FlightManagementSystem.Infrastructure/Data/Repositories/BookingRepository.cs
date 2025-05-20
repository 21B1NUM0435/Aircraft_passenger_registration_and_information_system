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
    public class BookingRepository : GenericRepository<Booking>, IBookingRepository
    {
        public BookingRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Booking?> GetByReferenceWithDetailsAsync(string bookingReference)
        {
            return await _context.Bookings
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Include(b => b.SeatAssignments)
                .Include(b => b.BoardingPasses)
                .FirstOrDefaultAsync(b => b.BookingReference == bookingReference);
        }

        public async Task<IEnumerable<Booking>> GetBookingsByPassengerIdAsync(string passengerId)
        {
            return await _context.Bookings
                .Where(b => b.PassengerId == passengerId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Booking>> GetBookingsByFlightNumberAsync(string flightNumber)
        {
            return await _context.Bookings
                .Where(b => b.FlightNumber == flightNumber)
                .ToListAsync();
        }

        public async Task<Booking?> GetBookingByPassportAndFlightAsync(string passportNumber, string flightNumber)
        {
            return await _context.Bookings
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Where(b => b.Passenger.PassportNumber == passportNumber && b.FlightNumber == flightNumber)
                .FirstOrDefaultAsync();
        }
    }
}
