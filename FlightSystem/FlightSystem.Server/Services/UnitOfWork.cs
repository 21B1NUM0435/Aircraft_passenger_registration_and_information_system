using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly FlightDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;
    private bool _disposed = false;

    public UnitOfWork(
        FlightDbContext context,
        IFlightRepository flightRepository,
        IPassengerRepository passengerRepository,
        ISeatRepository seatRepository,
        IBookingRepository bookingRepository,
        ILogger<UnitOfWork> logger)
    {
        _context = context;
        _logger = logger;

        Flights = flightRepository;
        Passengers = passengerRepository;
        Seats = seatRepository;
        Bookings = bookingRepository;
    }

    public IFlightRepository Flights { get; }
    public IPassengerRepository Passengers { get; }
    public ISeatRepository Seats { get; }
    public IBookingRepository Bookings { get; }

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes to database");
            throw;
        }
    }

    public async Task BeginTransactionAsync()
    {
        try
        {
            _transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogDebug("Database transaction started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting database transaction");
            throw;
        }
    }

    public async Task CommitTransactionAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                _logger.LogDebug("Database transaction committed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing database transaction");
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                _logger.LogDebug("Database transaction rolled back");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back database transaction");
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _disposed = true;
        }
    }
}