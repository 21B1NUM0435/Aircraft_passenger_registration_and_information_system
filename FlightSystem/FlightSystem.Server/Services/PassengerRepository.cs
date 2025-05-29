using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public class PassengerRepository : Repository<Passenger>, IPassengerRepository
{
    public PassengerRepository(FlightDbContext context, ILogger<PassengerRepository> logger)
        : base(context, logger)
    {
    }

    public async Task<Passenger?> GetByPassportNumberAsync(string passportNumber)
    {
        try
        {
            return await _dbSet
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.Flight)
                .FirstOrDefaultAsync(p => p.PassportNumber == passportNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passenger by passport {PassportNumber}", passportNumber);
            throw;
        }
    }

    public async Task<List<Passenger>> SearchByNameAsync(string firstName, string lastName)
    {
        try
        {
            return await _dbSet
                .Where(p => p.FirstName.Contains(firstName) && p.LastName.Contains(lastName))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching passengers by name {FirstName} {LastName}", firstName, lastName);
            throw;
        }
    }

    public async Task<List<Booking>> GetPassengerBookingsAsync(string passportNumber)
    {
        try
        {
            var passenger = await _dbSet
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.Flight)
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.Seat)
                .FirstOrDefaultAsync(p => p.PassportNumber == passportNumber);

            return passenger?.Bookings.ToList() ?? new List<Booking>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookings for passenger {PassportNumber}", passportNumber);
            throw;
        }
    }
}