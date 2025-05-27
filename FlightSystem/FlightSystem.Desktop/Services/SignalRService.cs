using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace FlightSystem.Desktop.Services;

public class SignalRService : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly System.Threading.Timer _heartbeatTimer;
    private bool _disposed = false;

    // Events for UI updates - using weak event pattern to prevent memory leaks
    private readonly List<WeakReference<Action<string>>> _connectionStatusHandlers = new();
    private readonly List<WeakReference<Action<FlightStatusUpdate>>> _flightStatusHandlers = new();
    private readonly List<WeakReference<Action<SeatAssignmentUpdate>>> _seatAssignmentHandlers = new();

    public event Action<string> ConnectionStatusChanged
    {
        add => _connectionStatusHandlers.Add(new WeakReference<Action<string>>(value));
        remove => RemoveHandler(_connectionStatusHandlers, value);
    }

    public event Action<FlightStatusUpdate> FlightStatusChanged
    {
        add => _flightStatusHandlers.Add(new WeakReference<Action<FlightStatusUpdate>>(value));
        remove => RemoveHandler(_flightStatusHandlers, value);
    }

    public event Action<SeatAssignmentUpdate> SeatAssigned
    {
        add => _seatAssignmentHandlers.Add(new WeakReference<Action<SeatAssignmentUpdate>>(value));
        remove => RemoveHandler(_seatAssignmentHandlers, value);
    }

    public SignalRService(string serverUrl)
    {
        _cancellationTokenSource = new CancellationTokenSource();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/flighthub", options =>
            {
                // Only for development - remove in production
                if (serverUrl.Contains("localhost"))
                {
                    options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                    };
                }
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        SetupEventHandlers();

        // Heartbeat timer to maintain connection and clean up weak references
        _heartbeatTimer = new System.Threading.Timer(
            async _ => await PerformHeartbeatAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    private void RemoveHandler<T>(List<WeakReference<T>> handlers, T handler) where T : class
    {
        for (int i = handlers.Count - 1; i >= 0; i--)
        {
            if (!handlers[i].TryGetTarget(out var target) || ReferenceEquals(target, handler))
            {
                handlers.RemoveAt(i);
            }
        }
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRService));

        try
        {
            Console.WriteLine("🔌 Connecting to SignalR hub...");
            InvokeConnectionStatusChanged("Connecting...");

            await _connection.StartAsync(_cancellationTokenSource.Token);

            Console.WriteLine("✅ Connected to SignalR hub");
            InvokeConnectionStatusChanged("Connected");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to connect to SignalR hub: {ex.Message}");
            InvokeConnectionStatusChanged($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed || _connection.State == HubConnectionState.Disconnected)
            return;

        try
        {
            await _connection.StopAsync(CancellationToken.None);
            Console.WriteLine("🔌 Disconnected from SignalR hub");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error disconnecting from SignalR hub: {ex.Message}");
        }
    }

    public async Task SendPingAsync()
    {
        if (_disposed || _connection.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("Ping", _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending ping: {ex.Message}");
        }
    }

    public async Task JoinFlightGroupAsync(string flightNumber)
    {
        if (_disposed || _connection.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("JoinFlightGroup", flightNumber, _cancellationTokenSource.Token);
            Console.WriteLine($"👥 Joined flight group: {flightNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error joining flight group {flightNumber}: {ex.Message}");
        }
    }

    public async Task LeaveFlightGroupAsync(string flightNumber)
    {
        if (_disposed || _connection.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("LeaveFlightGroup", flightNumber, _cancellationTokenSource.Token);
            Console.WriteLine($"👋 Left flight group: {flightNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error leaving flight group {flightNumber}: {ex.Message}");
        }
    }

    public string GetConnectionState()
    {
        return _connection.State.ToString();
    }

    private async Task PerformHeartbeatAsync()
    {
        if (_disposed)
            return;

        // Clean up dead weak references
        CleanupWeakReferences();

        // Send heartbeat if connected
        if (_connection.State == HubConnectionState.Connected)
        {
            await SendPingAsync();
        }
    }

    private void CleanupWeakReferences()
    {
        CleanupWeakReferenceList(_connectionStatusHandlers);
        CleanupWeakReferenceList(_flightStatusHandlers);
        CleanupWeakReferenceList(_seatAssignmentHandlers);
    }

    private void CleanupWeakReferenceList<T>(List<WeakReference<T>> handlers) where T : class
    {
        for (int i = handlers.Count - 1; i >= 0; i--)
        {
            if (!handlers[i].TryGetTarget(out _))
            {
                handlers.RemoveAt(i);
            }
        }
    }

    // Event handlers
    private Task OnDisconnected(Exception? exception)
    {
        Console.WriteLine($"📪 SignalR connection lost: {exception?.Message ?? "Unknown reason"}");
        InvokeConnectionStatusChanged("Disconnected");
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        Console.WriteLine("🔄 SignalR reconnecting...");
        InvokeConnectionStatusChanged("Reconnecting...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        Console.WriteLine($"✅ SignalR reconnected with ID: {connectionId}");
        InvokeConnectionStatusChanged("Connected");
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
        InvokeFlightStatusChanged(update);
        return Task.CompletedTask;
    }

    private Task OnSeatAssigned(SeatAssignmentUpdate update)
    {
        Console.WriteLine($"🪑 Seat assigned: {update.SeatNumber} to {update.PassengerName}");
        InvokeSeatAssigned(update);
        return Task.CompletedTask;
    }

    private Task OnPong(DateTime serverTime)
    {
        Console.WriteLine($"🏓 Pong received at {serverTime}");
        return Task.CompletedTask;
    }

    // Invoke methods with weak reference handling
    private void InvokeConnectionStatusChanged(string status)
    {
        InvokeWeakHandlers(_connectionStatusHandlers, handler => handler(status));
    }

    private void InvokeFlightStatusChanged(FlightStatusUpdate update)
    {
        InvokeWeakHandlers(_flightStatusHandlers, handler => handler(update));
    }

    private void InvokeSeatAssigned(SeatAssignmentUpdate update)
    {
        InvokeWeakHandlers(_seatAssignmentHandlers, handler => handler(update));
    }

    private void InvokeWeakHandlers<T>(List<WeakReference<T>> handlers, Action<T> invoke) where T : class
    {
        for (int i = handlers.Count - 1; i >= 0; i--)
        {
            if (handlers[i].TryGetTarget(out var handler))
            {
                try
                {
                    invoke(handler);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error invoking event handler: {ex.Message}");
                    // Remove problematic handler
                    handlers.RemoveAt(i);
                }
            }
            else
            {
                // Remove dead reference
                handlers.RemoveAt(i);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Cancel all operations
            _cancellationTokenSource.Cancel();

            // Dispose timer
            _heartbeatTimer?.Dispose();

            // Disconnect and dispose connection
            if (_connection.State != HubConnectionState.Disconnected)
            {
                await _connection.StopAsync();
            }
            await _connection.DisposeAsync();

            // Clear event handlers
            _connectionStatusHandlers.Clear();
            _flightStatusHandlers.Clear();
            _seatAssignmentHandlers.Clear();

            // Dispose cancellation token source
            _cancellationTokenSource.Dispose();

            Console.WriteLine("🧹 SignalR service disposed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing SignalR service: {ex.Message}");
        }
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