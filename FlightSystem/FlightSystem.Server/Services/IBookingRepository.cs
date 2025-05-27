using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public interface IBookingRepository : IRepository<Booking>
{
    Task<Booking?> GetBookingWithDetailsAsync(string bookingReference);
    Task<Booking?> FindBookingAsync(string passportNumber, string flightNumber);
    Task<List<Booking>> GetBookingsByFlightAsync(string flightNumber);
    Task<List<Booking>> GetBookingsByStatusAsync(BookingStatus status);
    Task<bool> UpdateBookingStatusAsync(string bookingReference, BookingStatus newStatus);
}