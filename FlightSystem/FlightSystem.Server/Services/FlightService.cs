using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightSystem.Server.Services;

public interface IFlightService
{
    Task<List<Flight>> GetAllFlightsAsync();
    Task<Flight?> GetFlightAsync(string flightNumber);
    Task<List<Seat>> GetAvailableSeatsAsync(string flightNumber);
    Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus);
}

public class FlightService : IFlightService
{
    private readonly FlightDbContext _context;
    private readonly ILogger<FlightService> _logger;

    public FlightService(FlightDbContext context, ILogger<FlightService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Flight>> GetAllFlightsAsync()
    {
        try
        {
            return await _context.Flights
                .Include(f => f.Seats)
                .Include(f => f.Bookings)
                .ThenInclude(b => b.Passenger)
                .OrderBy(f => f.DepartureTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all flights");
            return new List<Flight>();
        }
    }

    public async Task<Flight?> GetFlightAsync(string flightNumber)
    {
        try
        {
            return await _context.Flights
                .Include(f => f.Seats)
                .Include(f => f.Bookings)
                .ThenInclude(b => b.Passenger)
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving flight {FlightNumber}", flightNumber);
            return null;
        }
    }

    public async Task<List<Seat>> GetAvailableSeatsAsync(string flightNumber)
    {
        try
        {
            return await _context.Seats
                .Where(s => s.FlightNumber == flightNumber && s.IsAvailable)
                .OrderBy(s => s.SeatNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available seats for flight {FlightNumber}", flightNumber);
            return new List<Seat>();
        }
    }

    public async Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus)
    {
        try
        {
            var flight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);
            if (flight == null)
            {
                _logger.LogWarning("Flight {FlightNumber} not found for status update", flightNumber);
                return false;
            }

            flight.Status = newStatus;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Flight {FlightNumber} status updated to {Status}", flightNumber, newStatus);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating flight {FlightNumber} status", flightNumber);
            return false;
        }
    }
}