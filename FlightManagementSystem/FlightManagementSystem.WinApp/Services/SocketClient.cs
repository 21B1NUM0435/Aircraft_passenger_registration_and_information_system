using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FlightManagementSystem.WinApp.Services
{
    public class SocketClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly string _serverAddress;
        private readonly int _serverPort;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenTask;
        private bool _isConnected = false;

        // Events for real-time notifications
        public event Action<string, string>? OnSeatReserved; // seatId, flightNumber
        public event Action<string, string>? OnFlightStatusChanged; // flightNumber, newStatus
        public event Action<string>? OnConnectionStatusChanged; // status message

        public SocketClient(string serverAddress, int serverPort)
        {
            _serverAddress = serverAddress;
            _serverPort = serverPort;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_serverAddress, _serverPort);

                _stream = _tcpClient.GetStream();
                _cancellationTokenSource = new CancellationTokenSource();

                // Start listening for messages
                _listenTask = Task.Run(async () => await ListenForMessagesAsync(_cancellationTokenSource.Token));

                _isConnected = true;
                OnConnectionStatusChanged?.Invoke("Connected to server");

                return true;
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _isConnected = false;
                _cancellationTokenSource?.Cancel();

                _stream?.Close();
                _tcpClient?.Close();

                OnConnectionStatusChanged?.Invoke("Disconnected from server");
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Disconnect error: {ex.Message}");
            }
        }

        public async Task NotifySeatReservedAsync(string seatId, string flightNumber)
        {
            if (!_isConnected) return;

            var message = new SocketMessage
            {
                Type = MessageType.SeatAssignment,
                Data = new SeatReservationMessage
                {
                    SeatId = seatId,
                    FlightNumber = flightNumber,
                    IsReserved = true,
                    Timestamp = DateTime.UtcNow
                },
                Timestamp = DateTime.UtcNow
            };

            await SendMessageAsync(message);
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
        }

        private async Task SendMessageAsync(SocketMessage message)
        {
            if (_stream == null || !_isConnected) return;

            try
            {
                var json = JsonSerializer.Serialize(message);
                var data = Encoding.UTF8.GetBytes(json);

                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Send error: {ex.Message}");
            }
        }

        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isConnected && _stream != null)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        // Server disconnected
                        break;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    try
                    {
                        var message = JsonSerializer.Deserialize<SocketMessage>(json);
                        await ProcessMessageAsync(message);
                    }
                    catch (JsonException ex)
                    {
                        // Log JSON parsing error but continue listening
                        OnConnectionStatusChanged?.Invoke($"Message parse error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Listen error: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
            }
        }

        private async Task ProcessMessageAsync(SocketMessage? message)
        {
            if (message == null) return;

            await Task.Run(() =>
            {
                try
                {
                    switch (message.Type)
                    {
                        case MessageType.SeatAssignment:
                            if (message.Data is JsonElement seatElement)
                            {
                                var seatMsg = JsonSerializer.Deserialize<SeatReservationMessage>(seatElement.GetRawText());
                                if (seatMsg != null && seatMsg.IsReserved)
                                {
                                    OnSeatReserved?.Invoke(seatMsg.SeatId, seatMsg.FlightNumber);
                                }
                            }
                            break;

                        case MessageType.FlightStatusUpdate:
                            if (message.Data is JsonElement flightElement)
                            {
                                var flightMsg = JsonSerializer.Deserialize<FlightStatusMessage>(flightElement.GetRawText());
                                if (flightMsg != null)
                                {
                                    OnFlightStatusChanged?.Invoke(flightMsg.FlightNumber, flightMsg.NewStatus);
                                }
                            }
                            break;

                        case MessageType.Ping:
                            // Respond to ping
                            var pongMessage = new SocketMessage
                            {
                                Type = MessageType.Ping,
                                Data = "Pong",
                                Timestamp = DateTime.UtcNow
                            };
                            _ = SendMessageAsync(pongMessage);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    OnConnectionStatusChanged?.Invoke($"Message processing error: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            Disconnect();
            _cancellationTokenSource?.Dispose();
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }
    }

    // Message classes for socket communication
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

    public class FlightStatusMessage
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum MessageType
    {
        SeatAssignment,
        FlightStatusUpdate,
        CheckInComplete,
        Error,
        Ping
    }
}