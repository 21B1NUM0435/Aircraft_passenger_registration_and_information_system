using Microsoft.AspNetCore.SignalR;

namespace FlightSystem.Server.Hubs;

public class FlightHub : Hub
{
    private readonly ILogger<FlightHub> _logger;

    public FlightHub(ILogger<FlightHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("✅ Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Send welcome message
        await Clients.Caller.SendAsync("Welcome", new 
        { 
            message = "Connected to Flight Management System",
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("❌ Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Join a flight group for targeted updates
    public async Task JoinFlightGroup(string flightNumber)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Flight_{flightNumber}");
        _logger.LogInformation("👥 Client {ConnectionId} joined flight group {FlightNumber}", 
            Context.ConnectionId, flightNumber);
    }

    // Leave a flight group
    public async Task LeaveFlightGroup(string flightNumber)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Flight_{flightNumber}");
        _logger.LogInformation("👋 Client {ConnectionId} left flight group {FlightNumber}", 
            Context.ConnectionId, flightNumber);
    }

    // Handle ping from clients
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    // Get server status
    public async Task GetStatus()
    {
        await Clients.Caller.SendAsync("ServerStatus", new
        {
            serverTime = DateTime.UtcNow,
            uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime
        });
    }
}