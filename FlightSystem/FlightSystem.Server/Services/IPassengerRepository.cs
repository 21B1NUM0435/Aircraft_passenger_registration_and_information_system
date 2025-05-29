using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public interface IPassengerRepository : IRepository<Passenger>
{
    Task<Passenger?> GetByPassportNumberAsync(string passportNumber);
    Task<List<Passenger>> SearchByNameAsync(string firstName, string lastName);
    Task<List<Booking>> GetPassengerBookingsAsync(string passportNumber);
}