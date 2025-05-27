using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface ICheckInService
    {
        Task<Booking?> FindBookingByPassportAsync(string passportNumber, string flightNumber);
        Task<IEnumerable<Seat>> GetAvailableSeatsAsync(string flightNumber);
        Task<(bool Success, string Message, BoardingPass? BoardingPass)> CheckInPassengerAsync(
            string bookingReference,
            string seatId,
            string staffId,
            string counterId);
        Task<bool> IsSeatAvailableAsync(string seatId, string flightNumber);
    }
}
