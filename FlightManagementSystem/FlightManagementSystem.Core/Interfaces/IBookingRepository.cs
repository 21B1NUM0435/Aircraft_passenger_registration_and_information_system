using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IBookingRepository : IRepository<Booking>
    {
        Task<Booking?> GetByReferenceWithDetailsAsync(string bookingReference);
        Task<IEnumerable<Booking>> GetBookingsByPassengerIdAsync(string passengerId);
        Task<IEnumerable<Booking>> GetBookingsByFlightNumberAsync(string flightNumber);
        Task<Booking?> GetBookingByPassportAndFlightAsync(string passportNumber, string flightNumber);
    }
}
