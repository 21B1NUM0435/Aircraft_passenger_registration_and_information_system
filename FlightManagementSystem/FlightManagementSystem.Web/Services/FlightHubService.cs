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
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying flight status change for {FlightNumber}", flightNumber);
            }
        }

        public async Task NotifySeatAssigned(string flightNumber, string seatId, bool isAssigned)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying seat assignment for {FlightNumber}", flightNumber);
            }
        }

        public async Task NotifyPassengerCheckedIn(string flightNumber, string passengerName)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying passenger check-in for {FlightNumber}", flightNumber);
            }
        }

        public async Task NotifyBoardingStarted(string flightNumber, string gate)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying boarding start for {FlightNumber}", flightNumber);
            }
        }
    }
}