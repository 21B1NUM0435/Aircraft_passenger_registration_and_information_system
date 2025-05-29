using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public class FlightRepository : Repository<Flight>, IFlightRepository
{
    public FlightRepository(FlightDbContext context, ILogger<FlightRepository> logger)
        : base(context, logger)
    {
    }

    public async Task<Flight?> GetFlightWithDetailsAsync(string flightNumber)
    {
        try
        {
            return await _dbSet
                .Include(f => f.Seats)
                .Include(f => f.Bookings)
                    .ThenInclude(b => b.Passenger)
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flight details for {FlightNumber}", flightNumber);
            throw;
        }
    }

    public async Task<List<Flight>> GetFlightsByStatusAsync(FlightStatus status)
    {
        try
        {
            return await _dbSet
                .Where(f => f.Status == status)
                .OrderBy(f => f.DepartureTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flights by status {Status}", status);
            throw;
        }
    }

    public async Task<List<Flight>> GetFlightsByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            return await _dbSet
                .Where(f => f.DepartureTime >= fromDate && f.DepartureTime <= toDate)
                .OrderBy(f => f.DepartureTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flights by date range {FromDate} - {ToDate}", fromDate, toDate);
            throw;
        }
    }

    public async Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus)
    {
        try
        {
            var flight = await _dbSet.FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);
            if (flight == null)
                return false;

            flight.Status = newStatus;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating flight status for {FlightNumber}", flightNumber);
            throw;
        }
    }
}