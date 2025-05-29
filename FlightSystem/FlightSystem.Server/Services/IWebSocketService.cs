using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace FlightSystem.Server.Services;

public interface IWebSocketService
{
    Task StartAsync();
    Task StopAsync();
    Task HandleWebSocketAsync(HttpContext context);
    Task BroadcastSeatLockAsync(string seatId, string flightNumber, bool isLocked, string lockedBy);
    Task BroadcastFlightStatusAsync(string flightNumber, string newStatus);
    Task NotifySpecificClientAsync(string clientId, object message);
}

public class WebSocketService : IWebSocketService
{
    private readonly ILogger<WebSocketService> _logger;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
    private readonly ConcurrentDictionary<string, string> _seatLocks = new(); // SeatId -> ClientId
    private readonly Timer _heartbeatTimer;

    public WebSocketService(ILogger<WebSocketService> logger)
    {
        _logger = logger;

        // Heartbeat timer to check connection health
        _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(),
            null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public Task StartAsync()
    {
        _logger.LogInformation("🔌 WebSocket service started");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _heartbeatTimer?.Dispose();

        // Close all connections
        var closeTasks = _connections.Values.Select(conn => CloseConnectionAsync(conn, "Server shutting down"));
        return Task.WhenAll(closeTasks);
    }

    public async Task HandleWebSocketAsync(HttpContext context)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();

        var connection = new WebSocketConnection
        {
            Id = connectionId,
            WebSocket = webSocket,
            ConnectedAt = DateTime.UtcNow,
            LastPingAt = DateTime.UtcNow
        };

        _connections[connectionId] = connection;

        _logger.LogInformation("✅ WebSocket client connected: {ConnectionId}", connectionId);

        // Send welcome message
        await SendMessageAsync(connection, new
        {
            Type = "Welcome",
            ConnectionId = connectionId,
            Message = "Connected to Flight Management System WebSocket",
            Timestamp = DateTime.UtcNow
        });

        try
        {
            await HandleConnectionAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error handling WebSocket connection {ConnectionId}", connectionId);
        }
        finally
        {
            await RemoveConnectionAsync(connectionId);
        }
    }

    private async Task HandleConnectionAsync(WebSocketConnection connection)
    {
        var buffer = new byte[4096];

        while (connection.WebSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await connection.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(connection, messageJson);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning("🔌 WebSocket connection {ConnectionId} closed: {Message}", connection.Id, ex.Message);
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(WebSocketConnection connection, string messageJson)
    {
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("Type", out var typeElement))
                return;

            var messageType = typeElement.GetString();

            switch (messageType)
            {
                case "Ping":
                    connection.LastPingAt = DateTime.UtcNow;
                    await SendMessageAsync(connection, new { Type = "Pong", Timestamp = DateTime.UtcNow });
                    break;

                case "RequestSeatLock":
                    await HandleSeatLockRequestAsync(connection, root);
                    break;

                case "ReleaseSeatLock":
                    await HandleSeatLockReleaseAsync(connection, root);
                    break;

                case "JoinFlightGroup":
                    if (root.TryGetProperty("FlightNumber", out var flightElement))
                    {
                        connection.FlightNumber = flightElement.GetString();
                        _logger.LogInformation("👥 Client {ConnectionId} joined flight group {FlightNumber}",
                            connection.Id, connection.FlightNumber);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing WebSocket message from {ConnectionId}", connection.Id);
        }
    }

    private async Task HandleSeatLockRequestAsync(WebSocketConnection connection, JsonElement message)
    {
        if (!message.TryGetProperty("SeatId", out var seatIdElement) ||
            !message.TryGetProperty("FlightNumber", out var flightElement))
            return;

        var seatId = seatIdElement.GetString();
        var flightNumber = flightElement.GetString();

        if (string.IsNullOrEmpty(seatId) || string.IsNullOrEmpty(flightNumber))
            return;

        // Try to acquire lock
        var lockAcquired = _seatLocks.TryAdd(seatId, connection.Id);

        if (lockAcquired)
        {
            _logger.LogInformation("🔒 Seat lock acquired: {SeatId} by {ConnectionId}", seatId, connection.Id);

            // Notify requesting client
            await SendMessageAsync(connection, new
            {
                Type = "SeatLockAcquired",
                SeatId = seatId,
                FlightNumber = flightNumber,
                Timestamp = DateTime.UtcNow
            });

            // Notify other clients in the same flight
            await BroadcastToFlightAsync(flightNumber, new
            {
                Type = "SeatLocked",
                SeatId = seatId,
                FlightNumber = flightNumber,
                LockedBy = connection.Id,
                Timestamp = DateTime.UtcNow
            }, connection.Id);
        }
        else
        {
            // Lock already held by another client
            var lockHolder = _seatLocks.GetValueOrDefault(seatId);

            await SendMessageAsync(connection, new
            {
                Type = "SeatLockDenied",
                SeatId = seatId,
                FlightNumber = flightNumber,
                LockedBy = lockHolder,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task HandleSeatLockReleaseAsync(WebSocketConnection connection, JsonElement message)
    {
        if (!message.TryGetProperty("SeatId", out var seatIdElement) ||
            !message.TryGetProperty("FlightNumber", out var flightElement))
            return;

        var seatId = seatIdElement.GetString();
        var flightNumber = flightElement.GetString();

        if (string.IsNullOrEmpty(seatId) || string.IsNullOrEmpty(flightNumber))
            return;

        // Release lock if held by this connection
        if (_seatLocks.TryGetValue(seatId, out var lockHolder) && lockHolder == connection.Id)
        {
            _seatLocks.TryRemove(seatId, out _);

            _logger.LogInformation("🔓 Seat lock released: {SeatId} by {ConnectionId}", seatId, connection.Id);

            // Notify all clients in the flight
            await BroadcastToFlightAsync(flightNumber, new
            {
                Type = "SeatUnlocked",
                SeatId = seatId,
                FlightNumber = flightNumber,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task BroadcastSeatLockAsync(string seatId, string flightNumber, bool isLocked, string lockedBy)
    {
        await BroadcastToFlightAsync(flightNumber, new
        {
            Type = isLocked ? "SeatLocked" : "SeatUnlocked",
            SeatId = seatId,
            FlightNumber = flightNumber,
            LockedBy = isLocked ? lockedBy : null,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastFlightStatusAsync(string flightNumber, string newStatus)
    {
        await BroadcastToFlightAsync(flightNumber, new
        {
            Type = "FlightStatusChanged",
            FlightNumber = flightNumber,
            NewStatus = newStatus,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task NotifySpecificClientAsync(string clientId, object message)
    {
        if (_connections.TryGetValue(clientId, out var connection))
        {
            await SendMessageAsync(connection, message);
        }
    }

    private async Task BroadcastToFlightAsync(string flightNumber, object message, string? excludeConnectionId = null)
    {
        var tasks = _connections.Values
            .Where(conn => conn.FlightNumber == flightNumber && conn.Id != excludeConnectionId)
            .Select(conn => SendMessageAsync(conn, message));

        await Task.WhenAll(tasks);
    }

    private async Task SendMessageAsync(WebSocketConnection connection, object message)
    {
        if (connection.WebSocket.State != WebSocketState.Open)
            return;

        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            await connection.WebSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebSocket message to {ConnectionId}", connection.Id);
            await RemoveConnectionAsync(connection.Id);
        }
    }

    private async Task SendHeartbeatAsync()
    {
        var disconnectedConnections = new List<string>();

        foreach (var connection in _connections.Values)
        {
            if (DateTime.UtcNow - connection.LastPingAt > TimeSpan.FromMinutes(2))
            {
                disconnectedConnections.Add(connection.Id);
            }
            else
            {
                await SendMessageAsync(connection, new { Type = "Heartbeat", Timestamp = DateTime.UtcNow });
            }
        }

        // Remove disconnected connections
        foreach (var connectionId in disconnectedConnections)
        {
            await RemoveConnectionAsync(connectionId);
        }
    }

    private async Task RemoveConnectionAsync(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            // Release any seat locks held by this connection
            var locksToRelease = _seatLocks
                .Where(kvp => kvp.Value == connectionId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var seatId in locksToRelease)
            {
                _seatLocks.TryRemove(seatId, out _);

                if (!string.IsNullOrEmpty(connection.FlightNumber))
                {
                    await BroadcastToFlightAsync(connection.FlightNumber, new
                    {
                        Type = "SeatUnlocked",
                        SeatId = seatId,
                        FlightNumber = connection.FlightNumber,
                        Reason = "Client disconnected",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            await CloseConnectionAsync(connection, "Connection removed");
            _logger.LogInformation("🔌 WebSocket client disconnected: {ConnectionId}", connectionId);
        }
    }

    private async Task CloseConnectionAsync(WebSocketConnection connection, string reason)
    {
        try
        {
            if (connection.WebSocket.State == WebSocketState.Open)
            {
                await connection.WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    reason,
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing WebSocket connection {ConnectionId}", connection.Id);
        }
    }
}

public class WebSocketConnection
{
    public string Id { get; set; } = string.Empty;
    public WebSocket WebSocket { get; set; } = null!;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastPingAt { get; set; }
    public string? FlightNumber { get; set; }
}