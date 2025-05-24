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
                _logger.LogInformation("📡 SignalR: Broadcasting flight status change: {FlightNumber} -> {Status}",
                    flightNumber, newStatus);

                var message = new
                {
                    FlightNumber = flightNumber,
                    NewStatus = newStatus.ToString(),
                    Timestamp = DateTime.UtcNow
                };

                // Broadcast to ALL connected clients
                await _hubContext.Clients.All.SendAsync("FlightStatusChanged", message);

                // Also broadcast to specific flight group if clients are subscribed
                var flightGroup = $"flight-{flightNumber}";
                await _hubContext.Clients.Group(flightGroup).SendAsync("FlightStatusChanged", message);

                _logger.LogInformation("✅ SignalR: Flight status broadcast completed for {FlightNumber}", flightNumber);
                Console.WriteLine($"✅ SignalR BROADCAST: Flight {flightNumber} status -> {newStatus}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error broadcasting flight status change for {FlightNumber}", flightNumber);
                Console.WriteLine($"❌ SignalR ERROR: Flight status broadcast failed - {ex.Message}");
            }
        }

        public async Task NotifySeatAssigned(string flightNumber, string seatId, bool isAssigned)
        {
            try
            {
                _logger.LogInformation("📡 SignalR: Broadcasting seat assignment: {SeatId} = {IsAssigned} for flight {FlightNumber}",
                    seatId, isAssigned, flightNumber);

                var message = new
                {
                    FlightNumber = flightNumber,
                    SeatId = seatId,
                    IsAssigned = isAssigned,
                    Timestamp = DateTime.UtcNow
                };

                // Broadcast to ALL connected clients
                await _hubContext.Clients.All.SendAsync("SeatAssigned", message);

                // Also broadcast to specific flight group
                var flightGroup = $"flight-{flightNumber}";
                await _hubContext.Clients.Group(flightGroup).SendAsync("SeatAssigned", message);

                _logger.LogInformation("✅ SignalR: Seat assignment broadcast completed for {SeatId}", seatId);
                Console.WriteLine($"✅ SignalR BROADCAST: Seat {seatId} assigned = {isAssigned}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error broadcasting seat assignment for {SeatId}", seatId);
                Console.WriteLine($"❌ SignalR ERROR: Seat assignment broadcast failed - {ex.Message}");
            }
        }

        public async Task NotifyPassengerCheckedIn(string flightNumber, string passengerName)
        {
            try
            {
                _logger.LogInformation("📡 SignalR: Broadcasting passenger check-in: {PassengerName} for flight {FlightNumber}",
                    passengerName, flightNumber);

                var message = new
                {
                    FlightNumber = flightNumber,
                    PassengerName = passengerName,
                    Timestamp = DateTime.UtcNow
                };

                // Broadcast to ALL connected clients
                await _hubContext.Clients.All.SendAsync("PassengerCheckedIn", message);

                // Also broadcast to specific flight group
                var flightGroup = $"flight-{flightNumber}";
                await _hubContext.Clients.Group(flightGroup).SendAsync("PassengerCheckedIn", message);

                _logger.LogInformation("✅ SignalR: Passenger check-in broadcast completed for {PassengerName}", passengerName);
                Console.WriteLine($"✅ SignalR BROADCAST: Passenger {passengerName} checked in");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error broadcasting passenger check-in for {PassengerName}", passengerName);
                Console.WriteLine($"❌ SignalR ERROR: Passenger check-in broadcast failed - {ex.Message}");
            }
        }

        public async Task NotifyBoardingStarted(string flightNumber, string gate)
        {
            try
            {
                _logger.LogInformation("📡 SignalR: Broadcasting boarding started: Flight {FlightNumber} at gate {Gate}",
                    flightNumber, gate);

                var message = new
                {
                    FlightNumber = flightNumber,
                    Gate = gate,
                    Timestamp = DateTime.UtcNow
                };

                // Broadcast to ALL connected clients
                await _hubContext.Clients.All.SendAsync("BoardingStarted", message);

                // Also broadcast to specific flight group
                var flightGroup = $"flight-{flightNumber}";
                await _hubContext.Clients.Group(flightGroup).SendAsync("BoardingStarted", message);

                _logger.LogInformation("✅ SignalR: Boarding started broadcast completed for flight {FlightNumber}", flightNumber);
                Console.WriteLine($"✅ SignalR BROADCAST: Boarding started for flight {flightNumber} at gate {gate}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error broadcasting boarding start for flight {FlightNumber}", flightNumber);
                Console.WriteLine($"❌ SignalR ERROR: Boarding start broadcast failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Send a general notification to all connected clients
        /// </summary>
        public async Task NotifyAllClients(string eventType, object data)
        {
            try
            {
                _logger.LogInformation("📡 SignalR: Broadcasting general notification: {EventType}", eventType);

                var message = new
                {
                    EventType = eventType,
                    Data = data,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("GeneralNotification", message);

                _logger.LogInformation("✅ SignalR: General notification broadcast completed: {EventType}", eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error broadcasting general notification: {EventType}", eventType);
            }
        }

        /// <summary>
        /// Get the count of connected SignalR clients (for diagnostics)
        /// </summary>
        public async Task<int> GetConnectedClientsCount()
        {
            try
            {
                // This is a workaround since IHubContext doesn't directly expose client count
                // In a real implementation, you might track this separately
                await _hubContext.Clients.All.SendAsync("Ping", new { timestamp = DateTime.UtcNow });
                return 0; // You would need to implement proper client tracking
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error getting client count");
                return 0;
            }
        }

        /// <summary>
        /// Send a test message to verify SignalR connectivity
        /// </summary>
        public async Task SendTestMessage()
        {
            try
            {
                _logger.LogInformation("🧪 SignalR: Sending test message");

                var testMessage = new
                {
                    Message = "SignalR Hub Test Message",
                    Timestamp = DateTime.UtcNow,
                    ServerInfo = Environment.MachineName
                };

                await _hubContext.Clients.All.SendAsync("TestMessage", testMessage);

                _logger.LogInformation("✅ SignalR: Test message sent successfully");
                Console.WriteLine("✅ SignalR TEST MESSAGE sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SignalR: Error sending test message");
                Console.WriteLine($"❌ SignalR TEST MESSAGE failed: {ex.Message}");
            }
        }
    }
}