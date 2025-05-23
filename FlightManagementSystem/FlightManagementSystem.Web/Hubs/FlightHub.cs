using Microsoft.AspNetCore.SignalR;

namespace FlightManagementSystem.Web.Hubs
{
    public class FlightHub : Hub
    {
        private readonly ILogger<FlightHub> _logger;

        public FlightHub(ILogger<FlightHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected to FlightHub: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected from FlightHub: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinFlightGroup(string flightNumber)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"flight-{flightNumber}");
            _logger.LogInformation("Client {ConnectionId} joined flight group {FlightNumber}", Context.ConnectionId, flightNumber);
        }

        public async Task LeaveFlightGroup(string flightNumber)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"flight-{flightNumber}");
            _logger.LogInformation("Client {ConnectionId} left flight group {FlightNumber}", Context.ConnectionId, flightNumber);
        }

        public Task SubscribeToFlightUpdates(string flightNumber)
        {
            _logger.LogInformation("Client {ConnectionId} subscribed to flight updates for {FlightNumber}", Context.ConnectionId, flightNumber);
            return JoinFlightGroup(flightNumber);
        }
    }
}