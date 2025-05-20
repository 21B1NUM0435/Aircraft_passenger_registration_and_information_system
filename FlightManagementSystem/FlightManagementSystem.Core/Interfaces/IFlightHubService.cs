using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IFlightHubService
    {
        Task NotifyFlightStatusChanged(string flightNumber, FlightStatus newStatus);
        Task NotifySeatAssigned(string flightNumber, string seatId, bool isAssigned);
        Task NotifyPassengerCheckedIn(string flightNumber, string passengerName);
        Task NotifyBoardingStarted(string flightNumber, string gate);
    }
}