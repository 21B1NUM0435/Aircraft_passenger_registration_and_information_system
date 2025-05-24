using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace FlightManagementSystem.Infrastructure.WebSocketServer
{
    public class FlightWebSocketServer : ISocketServer
    {
        private readonly ILogger<FlightWebSocketServer> _logger;
        private readonly int _port;
        private readonly ConcurrentDictionary<Guid, WebSocketConnection> _connections = new();
        private HttpListener? _httpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serverTask;

        // Message queue for reliable delivery
        private readonly ConcurrentQueue<BroadcastMessage> _messageQueue = new();
        private Task? _messageProcessorTask;

        // Statistics
        private long _totalMessagesSent = 0;
        private long _totalClientsConnected = 0;

        public FlightWebSocketServer(ILogger<FlightWebSocketServer> logger, int port = 8080)
        {
            _logger = logger;
            _port = port;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_port}/");
            _httpListener.Start();

            _logger.LogInformation("🚀 WebSocket server started on ws://localhost:{Port}", _port);
            Console.WriteLine($"🌐 WebSocket Server running on ws://localhost:{_port}");

            // Start message processor
            _messageProcessorTask = Task.Run(async () => await ProcessMessageQueueAsync(_cancellationTokenSource.Token));

            // Start accepting connections
            _serverTask = Task.Run(async () => await AcceptWebSocketsAsync(_cancellationTokenSource.Token));
        }

        public Task StopAsync()
        {
            _logger.LogInformation("🛑 Stopping WebSocket server");

            _cancellationTokenSource?.Cancel();
            _httpListener?.Stop();

            // Dispose all connections
            foreach (var connection in _connections.Values)
            {
                connection.Dispose();
            }
            _connections.Clear();

            _logger.LogInformation("✅ WebSocket server stopped. Stats: {TotalClients} clients served, {TotalMessages} messages sent",
                _totalClientsConnected, _totalMessagesSent);

            return Task.CompletedTask;
        }

        public void BroadcastMessage(string message)
        {
            var timestamp = DateTime.UtcNow;
            _logger.LogInformation("📡 Broadcasting WebSocket message to {ClientCount} clients at {Timestamp}",
                _connections.Count, timestamp.ToString("HH:mm:ss.fff"));

            Console.WriteLine($"📡 WEBSOCKET BROADCAST: {message}");

            // Add message to queue for reliable delivery
            _messageQueue.Enqueue(new BroadcastMessage
            {
                Content = message,
                Timestamp = timestamp,
                RetryCount = 0,
                Priority = GetMessagePriority(message)
            });
        }

        private async Task ProcessMessageQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔄 WebSocket message processor started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        await DeliverBroadcastMessage(message);
                    }
                    else
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing WebSocket message queue");
                }
            }
        }

        private async Task DeliverBroadcastMessage(BroadcastMessage message)
        {
            var deliveryStart = DateTime.UtcNow;
            var failedConnections = new List<Guid>();
            var successCount = 0;
            var connectionCount = _connections.Count;

            if (connectionCount == 0)
            {
                _logger.LogWarning("⚠️ No WebSocket clients connected - message not delivered");
                return;
            }

            // Parallel delivery for better performance
            var deliveryTasks = _connections.Values.Select(async connection =>
            {
                try
                {
                    await connection.SendAsync(message.Content);
                    return new { ConnectionId = connection.Id, Success = true, Error = (Exception?)null };
                }
                catch (Exception ex)
                {
                    return new { ConnectionId = connection.Id, Success = false, Error = ex };
                }
            });

            var results = await Task.WhenAll(deliveryTasks);

            foreach (var result in results)
            {
                if (result.Success)
                {
                    successCount++;
                    _totalMessagesSent++;
                }
                else
                {
                    failedConnections.Add(result.ConnectionId);
                    _logger.LogWarning("❌ Failed to deliver to connection {ConnectionId}: {Error}",
                        result.ConnectionId, result.Error?.Message);
                }
            }

            var deliveryTime = (DateTime.UtcNow - deliveryStart).TotalMilliseconds;
            _logger.LogInformation("✅ WebSocket message delivered: {Success}/{Total} clients in {DeliveryTime}ms",
                successCount, connectionCount, deliveryTime);

            // Clean up failed connections
            await CleanupFailedConnections(failedConnections);

            // Retry logic for important messages
            if (failedConnections.Any() && message.RetryCount < 3 && message.Priority > 0)
            {
                message.RetryCount++;
                var retryDelay = TimeSpan.FromMilliseconds(Math.Pow(2, message.RetryCount) * 500);

                _logger.LogInformation("🔄 Retrying WebSocket message delivery (attempt {RetryCount}) in {RetryDelay}ms",
                    message.RetryCount, retryDelay.TotalMilliseconds);

                await Task.Delay(retryDelay);
                _messageQueue.Enqueue(message);
            }
        }

        private async Task CleanupFailedConnections(List<Guid> failedConnectionIds)
        {
            foreach (var connectionId in failedConnectionIds)
            {
                if (_connections.TryRemove(connectionId, out var connection))
                {
                    connection.Dispose();
                    _logger.LogInformation("🧹 Removed failed WebSocket connection {ConnectionId}", connectionId);
                }
            }
        }

        private async Task AcceptWebSocketsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _httpListener != null)
                {
                    var context = await _httpListener.GetContextAsync();

                    if (context.Request.IsWebSocketRequest)
                    {
                        var webSocketContext = await context.AcceptWebSocketAsync(null);
                        var connectionId = Guid.NewGuid();

                        _totalClientsConnected++;
                        _logger.LogInformation("🎯 New WebSocket connection from {RemoteEndPoint} (Total: {TotalConnected})",
                            context.Request.RemoteEndPoint, _totalClientsConnected);

                        var connection = new WebSocketConnection(
                            connectionId,
                            webSocketContext.WebSocket,
                            _logger,
                            _connections,
                            context.Request.RemoteEndPoint?.ToString() ?? "Unknown");

                        _connections.TryAdd(connectionId, connection);

                        // Handle connection in background
                        _ = Task.Run(async () => await HandleConnectionAsync(connection, cancellationToken));
                    }
                    else
                    {
                        // Serve a simple WebSocket test page for debugging
                        await ServeTestPage(context);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 WebSocket server loop was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in WebSocket server loop");
            }
        }

        private async Task HandleConnectionAsync(WebSocketConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await connection.HandleAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling WebSocket connection {ConnectionId}", connection.Id);
            }
            finally
            {
                if (_connections.TryRemove(connection.Id, out _))
                {
                    connection.Dispose();
                    _logger.LogInformation("👋 WebSocket connection {ConnectionId} disconnected (Remaining: {RemainingConnections})",
                        connection.Id, _connections.Count);
                }
            }
        }

        private async Task ServeTestPage(HttpListenerContext context)
        {
            var testPageHtml = """
                <!DOCTYPE html>
                <html>
                <head><title>WebSocket Test</title></head>
                <body>
                    <h1>Flight Management WebSocket Server</h1>
                    <p>WebSocket server is running!</p>
                    <div id="status">Disconnected</div>
                    <button onclick="connect()">Connect</button>
                    <button onclick="testMessage()">Test Message</button>
                    <div id="messages"></div>
                    <script>
                        let ws;
                        function connect() {
                            ws = new WebSocket('ws://localhost:8080');
                            ws.onopen = () => document.getElementById('status').innerText = 'Connected';
                            ws.onclose = () => document.getElementById('status').innerText = 'Disconnected';
                            ws.onmessage = (e) => {
                                const div = document.createElement('div');
                                div.textContent = 'Received: ' + e.data;
                                document.getElementById('messages').appendChild(div);
                            };
                        }
                        function testMessage() {
                            if (ws) ws.send(JSON.stringify({type: 'Ping', data: 'Test', timestamp: new Date()}));
                        }
                    </script>
                </body>
                </html>
                """;

            var bytes = Encoding.UTF8.GetBytes(testPageHtml);
            context.Response.ContentLength64 = bytes.Length;
            context.Response.ContentType = "text/html";
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }

        private int GetMessagePriority(string message)
        {
            try
            {
                if (message.Contains("FlightStatusUpdate")) return 3; // High priority
                if (message.Contains("SeatLock")) return 2; // Medium priority
                if (message.Contains("SeatAssignment")) return 2; // Medium priority
                if (message.Contains("CheckInComplete")) return 1; // Low priority
                return 0; // Default priority
            }
            catch
            {
                return 0;
            }
        }

        public int GetConnectedClientsCount() => _connections.Count;

        public WebSocketServerStats GetStats()
        {
            return new WebSocketServerStats
            {
                ConnectedClients = _connections.Count,
                TotalClientsConnected = _totalClientsConnected,
                TotalMessagesSent = _totalMessagesSent,
                QueuedMessages = _messageQueue.Count
            };
        }
    }

    public class WebSocketConnection : IDisposable
    {
        public Guid Id { get; }
        public DateTime ConnectedAt { get; }
        public string RemoteEndPoint { get; }
        public HashSet<string> SubscribedFlights { get; } = new();

        private readonly WebSocket _webSocket;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, WebSocketConnection> _allConnections;
        private readonly SemaphoreSlim _sendSemaphore;
        private bool _disposed = false;

        public WebSocketConnection(
            Guid id,
            WebSocket webSocket,
            ILogger logger,
            ConcurrentDictionary<Guid, WebSocketConnection> allConnections,
            string remoteEndPoint)
        {
            Id = id;
            ConnectedAt = DateTime.UtcNow;
            RemoteEndPoint = remoteEndPoint;
            _webSocket = webSocket;
            _logger = logger;
            _allConnections = allConnections;
            _sendSemaphore = new SemaphoreSlim(1, 1);
        }

        public async Task HandleAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                // Send welcome message
                await SendWelcomeMessageAsync();

                while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger.LogDebug("📨 Received from WebSocket {ConnectionId}: {MessageType}", Id, GetMessageType(message));
                        await ProcessMessageAsync(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogDebug("📪 WebSocket {ConnectionId} requested close", Id);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("🛑 WebSocket {ConnectionId} operation cancelled", Id);
            }
            catch (WebSocketException wsEx)
            {
                _logger.LogDebug("📪 WebSocket {ConnectionId} disconnected: {Error}", Id, wsEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in WebSocket communication for {ConnectionId}", Id);
            }
        }

        private async Task ProcessMessageAsync(string message)
        {
            try
            {
                Console.WriteLine($"📨 WebSocket message from {Id}: {message}");

                var socketMessage = JsonSerializer.Deserialize<SocketMessage>(message);
                if (socketMessage == null) return;

                Console.WriteLine($"📋 Parsed WebSocket message type: {socketMessage.Type} from connection {Id}");

                switch (socketMessage.Type)
                {
                    case MessageType.Ping:
                        await SendPongResponseAsync();
                        break;

                    case MessageType.FlightSubscription:
                        await HandleFlightSubscriptionAsync(socketMessage);
                        break;

                    case MessageType.SeatAssignment:
                    case MessageType.FlightStatusUpdate:
                    case MessageType.SeatLock:
                    case MessageType.CheckInComplete:
                        // CRITICAL: Forward to other connections immediately
                        await ForwardMessageToOtherConnectionsAsync(socketMessage);
                        break;

                    default:
                        _logger.LogDebug("❓ Unknown WebSocket message type {MessageType} from connection {ConnectionId}",
                            socketMessage.Type, Id);
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("📝 Failed to parse WebSocket message from connection {ConnectionId}: {Error}", Id, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing WebSocket message from connection {ConnectionId}", Id);
            }
        }

        private async Task ForwardMessageToOtherConnectionsAsync(SocketMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                _logger.LogDebug("🔄 Forwarding WebSocket {MessageType} from connection {ConnectionId} to {OtherConnectionCount} other connections",
                    message.Type, Id, _allConnections.Count - 1);

                Console.WriteLine($"🔄 FORWARDING WebSocket {message.Type} from {Id} to {_allConnections.Count - 1} other connections");

                var forwardTasks = _allConnections.Values
                    .Where(c => c.Id != this.Id && !c._disposed)
                    .Select(async connection =>
                    {
                        try
                        {
                            await connection.SendAsync(json);
                            return new { ConnectionId = connection.Id, Success = true };
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("❌ Failed to forward WebSocket message to connection {ConnectionId}: {Error}",
                                connection.Id, ex.Message);
                            return new { ConnectionId = connection.Id, Success = false };
                        }
                    });

                var results = await Task.WhenAll(forwardTasks);
                var successCount = results.Count(r => r.Success);

                Console.WriteLine($"✅ WebSocket FORWARDED to {successCount}/{results.Length} connections");
                _logger.LogDebug("✅ WebSocket message forwarded to {Success}/{Total} connections", successCount, results.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error forwarding WebSocket message from connection {ConnectionId}", Id);
            }
        }

        private async Task HandleFlightSubscriptionAsync(SocketMessage message)
        {
            try
            {
                if (message.Data is JsonElement element && element.TryGetProperty("flightNumber", out var flightNumberProp))
                {
                    var flightNumber = flightNumberProp.GetString();
                    if (!string.IsNullOrEmpty(flightNumber))
                    {
                        SubscribedFlights.Add(flightNumber);
                        _logger.LogInformation("✈️ WebSocket connection {ConnectionId} subscribed to flight {FlightNumber}", Id, flightNumber);

                        // Send confirmation
                        var response = new SocketMessage
                        {
                            Type = MessageType.System,
                            Data = new { success = true, flightNumber = flightNumber },
                            Timestamp = DateTime.UtcNow
                        };

                        await SendAsync(JsonSerializer.Serialize(response));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling WebSocket flight subscription for connection {ConnectionId}", Id);
            }
        }

        private async Task SendWelcomeMessageAsync()
        {
            var welcomeMessage = new SocketMessage
            {
                Type = MessageType.System,
                Data = new
                {
                    message = "Connected to Flight Management WebSocket Server",
                    connectionId = Id,
                    serverTime = DateTime.UtcNow,
                    protocol = "WebSocket"
                },
                Timestamp = DateTime.UtcNow
            };

            await SendAsync(JsonSerializer.Serialize(welcomeMessage));
            _logger.LogDebug("👋 WebSocket welcome message sent to connection {ConnectionId}", Id);
        }

        private async Task SendPongResponseAsync()
        {
            var response = new SocketMessage
            {
                Type = MessageType.Ping,
                Data = "Pong",
                Timestamp = DateTime.UtcNow
            };

            await SendAsync(JsonSerializer.Serialize(response));
        }

        public async Task SendAsync(string message)
        {
            if (_webSocket.State != WebSocketState.Open || _disposed)
                return;

            await _sendSemaphore.WaitAsync();
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private string GetMessageType(string message)
        {
            try
            {
                var json = JsonDocument.Parse(message);
                return json.RootElement.GetProperty("type").GetString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _sendSemaphore?.Dispose();
                if (_webSocket.State == WebSocketState.Open)
                {
                    _ = _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutdown", CancellationToken.None);
                }
                _webSocket?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disposing WebSocket connection {ConnectionId}", Id);
            }

            _disposed = true;
        }
    }

    #region Supporting Classes

    public class BroadcastMessage
    {
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int RetryCount { get; set; }
        public int Priority { get; set; } = 0;
    }

    public class WebSocketServerStats
    {
        public int ConnectedClients { get; set; }
        public long TotalClientsConnected { get; set; }
        public long TotalMessagesSent { get; set; }
        public int QueuedMessages { get; set; }
    }

    #endregion
}