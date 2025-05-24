using FlightManagementSystem.Core.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace FlightManagementSystem.WinApp.Services
{
    public class WebSocketClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly string _serverUrl;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenTask;
        private bool _isConnected = false;
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

        // Enhanced events with better error handling
        public event Action<string, string>? OnSeatReserved; // seatId, flightNumber
        public event Action<string, string>? OnFlightStatusChanged; // flightNumber, newStatus
        public event Action<string>? OnConnectionStatusChanged; // status message
        public event Action<string, bool>? OnSeatLocked; // seatId, isLocked
        public event Action<string, string, string>? OnCheckInComplete; // flightNumber, passengerName, seatId

        // Connection health monitoring
        private readonly System.Threading.Timer _heartbeatTimer;
        private DateTime _lastMessageReceived = DateTime.UtcNow;
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(60);

        public WebSocketClient(string serverAddress, int serverPort)
        {
            _serverUrl = $"ws://{serverAddress}:{serverPort}";

            // Initialize heartbeat timer
            _heartbeatTimer = new System.Threading.Timer(CheckConnectionHealth, null, _heartbeatInterval, _heartbeatInterval);
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Console.WriteLine($"🔌 Connecting to WebSocket server {_serverUrl}");
                OnConnectionStatusChanged?.Invoke("Connecting...");

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                await _webSocket.ConnectAsync(new Uri(_serverUrl), _cancellationTokenSource.Token);

                // Start listening for messages
                _listenTask = Task.Run(async () => await ListenForMessagesAsync(_cancellationTokenSource.Token));

                _isConnected = true;
                _lastMessageReceived = DateTime.UtcNow;

                OnConnectionStatusChanged?.Invoke("Connected");
                Console.WriteLine("✅ WebSocket client connected successfully");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket connection failed: {ex.Message}");
                OnConnectionStatusChanged?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                Console.WriteLine("🔌 Disconnecting WebSocket client");
                _isConnected = false;

                _cancellationTokenSource?.Cancel();

                if (_webSocket?.State == WebSocketState.Open)
                {
                    _ = _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                }

                OnConnectionStatusChanged?.Invoke("Disconnected");
                Console.WriteLine("✅ WebSocket client disconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket disconnect error: {ex.Message}");
                OnConnectionStatusChanged?.Invoke($"Disconnect error: {ex.Message}");
            }
        }

        #region Message Sending Methods

        public async Task NotifySeatReservedAsync(string seatId, string flightNumber)
        {
            if (!_isConnected) return;

            var message = new SocketMessage
            {
                Type = MessageType.SeatAssignment,
                Data = new SeatAssignmentMessage
                {
                    SeatId = seatId,
                    FlightNumber = flightNumber,
                    IsAssigned = true,
                    PassengerName = "Reserved"
                },
                Timestamp = DateTime.UtcNow
            };

            await SendMessageAsync(message);
            Console.WriteLine($"📡 Sent WebSocket seat reservation: {seatId} for flight {flightNumber}");
        }

        public async Task NotifyFlightStatusChangedAsync(string flightNumber, string newStatus)
        {
            if (!_isConnected) return;

            var message = new SocketMessage
            {
                Type = MessageType.FlightStatusUpdate,
                Data = new FlightStatusMessage
                {
                    FlightNumber = flightNumber,
                    NewStatus = newStatus,
                    Timestamp = DateTime.UtcNow
                },
                Timestamp = DateTime.UtcNow
            };

            await SendMessageAsync(message);
            Console.WriteLine($"📡 Sent WebSocket flight status update: {flightNumber} -> {newStatus}");
        }

        public async Task SendPingAsync()
        {
            if (!_isConnected) return;

            var message = new SocketMessage
            {
                Type = MessageType.Ping,
                Data = "Ping",
                Timestamp = DateTime.UtcNow
            };

            await SendMessageAsync(message);
        }

        public async Task SubscribeToFlightAsync(string flightNumber)
        {
            if (!_isConnected) return;

            var message = new SocketMessage
            {
                Type = MessageType.FlightSubscription,
                Data = new { flightNumber = flightNumber },
                Timestamp = DateTime.UtcNow
            };

            await SendMessageAsync(message);
            Console.WriteLine($"📡 Subscribed to WebSocket flight updates: {flightNumber}");
        }

        #endregion

        private async Task SendMessageAsync(SocketMessage message)
        {
            if (_webSocket?.State != WebSocketState.Open || !_isConnected) return;

            await _sendSemaphore.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

                Console.WriteLine($"📤 Sent WebSocket message: {message.Type}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket send error: {ex.Message}");
                OnConnectionStatusChanged?.Invoke($"Send error: {ex.Message}");
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var messageBuffer = new List<byte>();

            try
            {
                Console.WriteLine("👂 Started listening for WebSocket messages");

                while (!cancellationToken.IsCancellationRequested &&
                       _isConnected &&
                       _webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Add received bytes to buffer
                        messageBuffer.AddRange(buffer.Take(result.Count));

                        // If this is the end of the message, process it
                        if (result.EndOfMessage)
                        {
                            var messageText = Encoding.UTF8.GetString(messageBuffer.ToArray());
                            messageBuffer.Clear();

                            await ProcessMessageAsync(messageText);
                            _lastMessageReceived = DateTime.UtcNow;
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("📪 WebSocket server requested close");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🛑 WebSocket message listening cancelled");
            }
            catch (WebSocketException wsEx)
            {
                Console.WriteLine($"📪 WebSocket connection closed: {wsEx.Message}");
                OnConnectionStatusChanged?.Invoke($"Connection closed: {wsEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket listen error: {ex.Message}");
                OnConnectionStatusChanged?.Invoke($"Listen error: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
                Console.WriteLine("👂 Stopped listening for WebSocket messages");
            }
        }

        private async Task ProcessMessageAsync(string json)
        {
            await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine($"📨 Processing WebSocket message: {json.Substring(0, Math.Min(100, json.Length))}...");

                    var message = JsonSerializer.Deserialize<SocketMessage>(json);
                    if (message == null) return;

                    Console.WriteLine($"📋 WebSocket message type: {message.Type}");

                    switch (message.Type)
                    {
                        case MessageType.SeatAssignment:
                            ProcessSeatAssignmentMessage(message.Data);
                            break;

                        case MessageType.FlightStatusUpdate:
                            ProcessFlightStatusMessage(message.Data);
                            break;

                        case MessageType.SeatLock:
                            ProcessSeatLockMessage(message.Data);
                            break;

                        case MessageType.CheckInComplete:
                            ProcessCheckInCompleteMessage(message.Data);
                            break;

                        case MessageType.Ping:
                            // Respond to ping
                            _ = Task.Run(async () => await SendPingAsync());
                            break;

                        case MessageType.System:
                            ProcessSystemMessage(message.Data);
                            break;

                        default:
                            Console.WriteLine($"❓ Unknown WebSocket message type: {message.Type}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ WebSocket message processing error: {ex.Message}");
                    OnConnectionStatusChanged?.Invoke($"Message processing error: {ex.Message}");
                }
            });
        }

        #region Message Processing Methods

        private void ProcessSeatAssignmentMessage(object data)
        {
            try
            {
                Console.WriteLine($"💺 Processing WebSocket seat assignment: {data}");

                string json = GetJsonFromData(data);
                var seatMsg = JsonSerializer.Deserialize<SeatAssignmentMessage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (seatMsg != null && seatMsg.IsAssigned)
                {
                    Console.WriteLine($"✅ WebSocket: Seat {seatMsg.SeatId} assigned on flight {seatMsg.FlightNumber}");
                    OnSeatReserved?.Invoke(seatMsg.SeatId, seatMsg.FlightNumber);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing WebSocket seat assignment: {ex.Message}");
            }
        }

        private void ProcessFlightStatusMessage(object data)
        {
            try
            {
                Console.WriteLine($"✈️ Processing WebSocket flight status: {data}");

                string json = GetJsonFromData(data);
                var flightMsg = JsonSerializer.Deserialize<FlightStatusMessage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (flightMsg != null)
                {
                    Console.WriteLine($"✅ WebSocket: Flight {flightMsg.FlightNumber} status changed to {flightMsg.NewStatus}");
                    OnFlightStatusChanged?.Invoke(flightMsg.FlightNumber, flightMsg.NewStatus);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing WebSocket flight status: {ex.Message}");
            }
        }

        private void ProcessSeatLockMessage(object data)
        {
            try
            {
                Console.WriteLine($"🔒 Processing WebSocket seat lock: {data}");

                string json = GetJsonFromData(data);
                var lockMsg = JsonSerializer.Deserialize<SeatLockMessage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (lockMsg != null)
                {
                    Console.WriteLine($"✅ WebSocket: Seat {lockMsg.SeatId} lock status: {lockMsg.IsLocked}");
                    OnSeatLocked?.Invoke(lockMsg.SeatId, lockMsg.IsLocked);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing WebSocket seat lock: {ex.Message}");
            }
        }

        private void ProcessCheckInCompleteMessage(object data)
        {
            try
            {
                Console.WriteLine($"🎫 Processing WebSocket check-in complete: {data}");

                string json = GetJsonFromData(data);
                var checkInMsg = JsonSerializer.Deserialize<CheckInCompleteMessage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (checkInMsg != null)
                {
                    Console.WriteLine($"✅ WebSocket: Check-in complete: {checkInMsg.PassengerName} on flight {checkInMsg.FlightNumber}");
                    OnCheckInComplete?.Invoke(checkInMsg.FlightNumber, checkInMsg.PassengerName, "");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing WebSocket check-in complete: {ex.Message}");
            }
        }

        private void ProcessSystemMessage(object data)
        {
            try
            {
                Console.WriteLine($"🔧 WebSocket system message: {data}");
                // Handle system messages like welcome, confirmations, etc.

                // Check if it's a welcome message
                if (data is JsonElement element &&
                    element.TryGetProperty("message", out var messageProp) &&
                    messageProp.GetString()?.Contains("Connected") == true)
                {
                    OnConnectionStatusChanged?.Invoke("Connected & Verified");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing WebSocket system message: {ex.Message}");
            }
        }

        #endregion

        private string GetJsonFromData(object data)
        {
            if (data is JsonElement element)
            {
                return element.GetRawText();
            }
            else
            {
                return JsonSerializer.Serialize(data);
            }
        }

        private void CheckConnectionHealth(object? state)
        {
            try
            {
                if (!_isConnected) return;

                var timeSinceLastMessage = DateTime.UtcNow - _lastMessageReceived;
                if (timeSinceLastMessage > _connectionTimeout)
                {
                    Console.WriteLine("💔 WebSocket connection timeout - attempting reconnection");
                    OnConnectionStatusChanged?.Invoke("Connection timeout - reconnecting...");

                    // Attempt reconnection
                    _ = Task.Run(async () =>
                    {
                        Disconnect();
                        await Task.Delay(2000); // Wait before reconnecting
                        await ConnectAsync();
                    });
                }
                else if (timeSinceLastMessage > _heartbeatInterval)
                {
                    // Send ping to keep connection alive
                    _ = Task.Run(async () => await SendPingAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket health check error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                _heartbeatTimer?.Dispose();
                Disconnect();
                _cancellationTokenSource?.Dispose();
                _sendSemaphore?.Dispose();
                _webSocket?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket dispose error: {ex.Message}");
            }
        }
    }

    #region Message Classes

    public class SeatAssignmentMessage
    {
        public string SeatId { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
        public string PassengerName { get; set; } = string.Empty;
    }

    public class FlightStatusMessage
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class SeatLockMessage
    {
        public string SeatId { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CheckInCompleteMessage
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string BookingReference { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
    }

    public class SocketMessage
    {
        public MessageType Type { get; set; }
        public object Data { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }

    public class SeatReservationMessage
    {
        public string SeatId { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public bool IsReserved { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum MessageType
    {
        SeatAssignment,
        FlightStatusUpdate,
        CheckInComplete,
        Error,
        Ping,
        SeatLock,
        System,
        FlightSubscription
    }

    #endregion
}