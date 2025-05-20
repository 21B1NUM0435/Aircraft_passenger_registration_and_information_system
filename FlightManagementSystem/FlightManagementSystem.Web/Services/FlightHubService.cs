using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using FlightManagementSystem.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FlightManagementSystem.Web.Services
{
    public class FlightHubService : IFlightHubService
    {
        private readonly IHubContext<FlightHub> _hubContext;
        private readonly ILogger<FlightHubService> _logger;

        public FlightHubService(IHubContext<FlightHub> hubContext, ILogger<FlightHubService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyFlightStatusChanged(string flightNumber, FlightStatus newStatus)
        {
            await _hubContext.Clients.All.SendAsync("FlightStatusChanged", new
            {
                FlightNumber = flightNumber,
                NewStatus = newStatus.ToString(),
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notified all clients of flight {FlightNumber} status change to {Status}",
                flightNumber, newStatus);
        }

        public async Task NotifySeatAssigned(string flightNumber, string seatId, bool isAssigned)
        {
            await _hubContext.Clients.Group($"flight-{flightNumber}").SendAsync("SeatAssigned", new
            {
                FlightNumber = flightNumber,
                SeatId = seatId,
                IsAssigned = isAssigned,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notified flight group {FlightNumber} of seat {SeatId} assignment",
                flightNumber, seatId);
        }

        public async Task NotifyPassengerCheckedIn(string flightNumber, string passengerName)
        {
            await _hubContext.Clients.Group($"flight-{flightNumber}").SendAsync("PassengerCheckedIn", new
            {
                FlightNumber = flightNumber,
                PassengerName = passengerName,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notified flight group {FlightNumber} of passenger {PassengerName} check-in",
                flightNumber, passengerName);
        }

        public async Task NotifyBoardingStarted(string flightNumber, string gate)
        {
            await _hubContext.Clients.Group($"flight-{flightNumber}").SendAsync("BoardingStarted", new
            {
                FlightNumber = flightNumber,
                Gate = gate,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notified flight group {FlightNumber} that boarding has started at gate {Gate}",
                flightNumber, gate);
        }
    }
}