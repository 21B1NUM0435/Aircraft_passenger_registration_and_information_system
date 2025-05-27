using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public interface IFlightRepository : IRepository<Flight>
{
    Task<Flight?> GetFlightWithDetailsAsync(string flightNumber);
    Task<List<Flight>> GetFlightsByStatusAsync(FlightStatus status);
    Task<List<Flight>> GetFlightsByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<bool> UpdateFlightStatusAsync(string flightNumber, FlightStatus newStatus);
}