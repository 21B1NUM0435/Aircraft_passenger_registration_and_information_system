using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(FlightDbContext context, ILogger<BookingRepository> logger)
        : base(context, logger)
    {
    }

    public async Task<Booking?> GetBookingWithDetailsAsync(string bookingReference)
    {
        try
        {
            return await _dbSet
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Include(b => b.Seat)
                .FirstOrDefaultAsync(b => b.BookingReference == bookingReference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking details for {BookingReference}", bookingReference);
            throw;
        }
    }

    public async Task<Booking?> FindBookingAsync(string passportNumber, string flightNumber)
    {
        try
        {
            return await _dbSet
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Include(b => b.Seat)
                .FirstOrDefaultAsync(b => b.PassportNumber == passportNumber && b.FlightNumber == flightNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding booking for passport {PassportNumber} on flight {FlightNumber}",
                passportNumber, flightNumber);
            throw;
        }
    }

    public async Task<List<Booking>> GetBookingsByFlightAsync(string flightNumber)
    {
        try
        {
            return await _dbSet
                .Include(b => b.Passenger)
                .Include(b => b.Seat)
                .Where(b => b.FlightNumber == flightNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookings for flight {FlightNumber}", flightNumber);
            throw;
        }
    }

    public async Task<List<Booking>> GetBookingsByStatusAsync(BookingStatus status)
    {
        try
        {
            return await _dbSet
                .Include(b => b.Passenger)
                .Include(b => b.Flight)
                .Where(b => b.Status == status)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookings by status {Status}", status);
            throw;
        }
    }

    public async Task<bool> UpdateBookingStatusAsync(string bookingReference, BookingStatus newStatus)
    {
        try
        {
            var booking = await _dbSet.FirstOrDefaultAsync(b => b.BookingReference == bookingReference);
            if (booking == null)
                return false;

            booking.Status = newStatus;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking status for {BookingReference}", bookingReference);
            throw;
        }
    }
}