using FlightSystem.Server.Data;
using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlightSystem.Server.Services;

public interface ISeatRepository : IRepository<Seat>
{
    Task<List<Seat>> GetAvailableSeatsAsync(string flightNumber);
    Task<List<Seat>> GetSeatsByClassAsync(string flightNumber, SeatClass seatClass);
    Task<bool> IsSeatAvailableAsync(string seatId);
    Task<bool> ReserveSeatAsync(string seatId);
    Task<bool> ReleaseSeatAsync(string seatId);
}