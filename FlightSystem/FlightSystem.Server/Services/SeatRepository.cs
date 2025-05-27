using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public class SeatRepository : Repository<Seat>, ISeatRepository
{
    public SeatRepository(FlightDbContext context, ILogger<SeatRepository> logger)
        : base(context, logger)
    {
    }

    public async Task<List<Seat>> GetAvailableSeatsAsync(string flightNumber)
    {
        try
        {
            return await _dbSet
                .Where(s => s.FlightNumber == flightNumber && s.IsAvailable)
                .OrderBy(s => s.SeatNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available seats for flight {FlightNumber}", flightNumber);
            throw;
        }
    }

    public async Task<List<Seat>> GetSeatsByClassAsync(string flightNumber, SeatClass seatClass)
    {
        try
        {
            return await _dbSet
                .Where(s => s.FlightNumber == flightNumber && s.Class == seatClass)
                .OrderBy(s => s.SeatNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting seats by class {SeatClass} for flight {FlightNumber}", seatClass, flightNumber);
            throw;
        }
    }

    public async Task<bool> IsSeatAvailableAsync(string seatId)
    {
        try
        {
            var seat = await _dbSet.FirstOrDefaultAsync(s => s.SeatId == seatId);
            return seat?.IsAvailable ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking seat availability for {SeatId}", seatId);
            throw;
        }
    }

    public async Task<bool> ReserveSeatAsync(string seatId)
    {
        try
        {
            var seat = await _dbSet.FirstOrDefaultAsync(s => s.SeatId == seatId);
            if (seat == null || !seat.IsAvailable)
                return false;

            seat.IsAvailable = false;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving seat {SeatId}", seatId);
            throw;
        }
    }

    public async Task<bool> ReleaseSeatAsync(string seatId)
    {
        try
        {
            var seat = await _dbSet.FirstOrDefaultAsync(s => s.SeatId == seatId);
            if (seat == null)
                return false;

            seat.IsAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing seat {SeatId}", seatId);
            throw;
        }
    }
}