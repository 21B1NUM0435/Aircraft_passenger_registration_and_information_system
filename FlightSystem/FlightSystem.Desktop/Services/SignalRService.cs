using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace FlightSystem.Desktop.Services;

public class SignalRService : IDisposable
{
    private readonly HubConnection _connection;

    // Events for UI updates
    public event Action<string>? ConnectionStatusChanged;
    public event Action<FlightStatusUpdate>? FlightStatusChanged;
    public event Action<SeatAssignmentUpdate>? SeatAssigned;

    public SignalRService(string serverUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/flighthub", options =>
            {
                // For development - ignore SSL certificate errors
                options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };
            })
            .WithAutomaticReconnect() // Auto-reconnect on disconnect
            .Build();

        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        // Connection events
        _connection.Closed += OnDisconnected;
        _connection.Reconnecting += OnReconnecting;
        _connection.Reconnected += OnReconnected;

        // Server message handlers
        _connection.On<object>("Welcome", OnWelcome);
        _connection.On<FlightStatusUpdate>("FlightStatusChanged", OnFlightStatusChanged);
        _connection.On<SeatAssignmentUpdate>("SeatAssigned", OnSeatAssigned);
        _connection.On<DateTime>("Pong", OnPong);
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            Console.WriteLine("🔌 Connecting to SignalR hub...");
            ConnectionStatusChanged?.Invoke("Connecting...");

            await _connection.StartAsync();

            Console.WriteLine("✅ Connected to SignalR hub");
            ConnectionStatusChanged?.Invoke("Connected");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to connect to SignalR hub: {ex.Message}");
            ConnectionStatusChanged?.Invoke($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await _connection.StopAsync();
            Console.WriteLine("🔌 Disconnected from SignalR hub");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error disconnecting from SignalR hub: {ex.Message}");
        }
    }

    public async Task SendPingAsync()
    {
        try
        {
            await _connection.InvokeAsync("Ping");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending ping: {ex.Message}");
        }
    }

    public async Task JoinFlightGroupAsync(string flightNumber)
    {
        try
        {
            await _connection.InvokeAsync("JoinFlightGroup", flightNumber);
            Console.WriteLine($"👥 Joined flight group: {flightNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error joining flight group {flightNumber}: {ex.Message}");
        }
    }

    public string GetConnectionState()
    {
        return _connection.State.ToString();
    }

    // Event handlers
    private Task OnDisconnected(Exception? exception)
    {
        Console.WriteLine("📪 SignalR connection lost");
        ConnectionStatusChanged?.Invoke("Disconnected");
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        Console.WriteLine("🔄 SignalR reconnecting...");
        ConnectionStatusChanged?.Invoke("Reconnecting...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        Console.WriteLine("✅ SignalR reconnected");
        ConnectionStatusChanged?.Invoke("Connected");
        return Task.CompletedTask;
    }

    private Task OnWelcome(object welcomeData)
    {
        Console.WriteLine($"👋 Received welcome message: {welcomeData}");
        return Task.CompletedTask;
    }

    private Task OnFlightStatusChanged(FlightStatusUpdate update)
    {
        Console.WriteLine($"✈️ Flight status changed: {update.FlightNumber} → {update.NewStatus}");
        FlightStatusChanged?.Invoke(update);
        return Task.CompletedTask;
    }

    private Task OnSeatAssigned(SeatAssignmentUpdate update)
    {
        Console.WriteLine($"🪑 Seat assigned: {update.SeatNumber} to {update.PassengerName}");
        SeatAssigned?.Invoke(update);
        return Task.CompletedTask;
    }

    private Task OnPong(DateTime serverTime)
    {
        Console.WriteLine($"🏓 Pong received at {serverTime}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _connection?.DisposeAsync();
    }
}

// Data models for SignalR messages
public class FlightStatusUpdate
{
    public string FlightNumber { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SeatAssignmentUpdate
{
    public string SeatId { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}