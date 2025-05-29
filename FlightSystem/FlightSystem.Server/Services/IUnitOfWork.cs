using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public interface IUnitOfWork : IDisposable
{
    IFlightRepository Flights { get; }
    IPassengerRepository Passengers { get; }
    ISeatRepository Seats { get; }
    IBookingRepository Bookings { get; }

    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}