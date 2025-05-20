using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace FlightManagementSystem.Infrastructure.SocketServer
{
    public class FlightSocketServer : ISocketServer
    {
        private readonly ILogger<FlightSocketServer> _logger;
        private readonly int _port;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();
        private Task? _serverTask;

        public FlightSocketServer(ILogger<FlightSocketServer> logger, int port = 5000)
        {
            _logger = logger;
            _port = port;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _logger.LogInformation("Socket server started on port {Port}", _port);

            _serverTask = Task.Run(async () => await AcceptClientsAsync(_cts.Token), _cts.Token);
        }

        public Task StopAsync()
        {
            _logger.LogInformation("Stopping socket server");

            _cts?.Cancel();
            _listener?.Stop();

            // Dispose all client connections
            foreach (var client in _clients.Values)
            {
                client.Dispose();
            }

            _clients.Clear();

            return Task.CompletedTask;
        }

        public void BroadcastMessage(string message)
        {
            _logger.LogDebug("Broadcasting message: {Message}", message);

            var deadClients = new List<Guid>();

            foreach (var client in _clients.Values)
            {
                try
                {
                    client.SendMessage(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message to client {ClientId}", client.Id);
                    deadClients.Add(client.Id);
                }
            }

            // Clean up dead clients
            foreach (var clientId in deadClients)
            {
                if (_clients.TryRemove(clientId, out var client))
                {
                    client.Dispose();
                    _logger.LogInformation("Removed dead client {ClientId}", clientId);
                }
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync(cancellationToken);
                    _logger.LogInformation("New client connected from {RemoteEndPoint}", tcpClient.Client.RemoteEndPoint);

                    var clientId = Guid.NewGuid();
                    var clientConnection = new ClientConnection(clientId, tcpClient, _logger);
                    _clients.TryAdd(clientId, clientConnection);

                    // Start a task to handle this client
                    _ = Task.Run(async () => await HandleClientAsync(clientConnection, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("Server loop was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in server loop");
            }
        }

        private async Task HandleClientAsync(ClientConnection client, CancellationToken cancellationToken)
        {
            try
            {
                await client.HandleCommunicationAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client {ClientId}", client.Id);
            }
            finally
            {
                if (_clients.TryRemove(client.Id, out _))
                {
                    client.Dispose();
                    _logger.LogInformation("Client {ClientId} disconnected", client.Id);
                }
            }
        }
    }

    // Client connection class to handle individual socket connections
    internal class ClientConnection : IDisposable
    {
        public Guid Id { get; }
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public ClientConnection(Guid id, TcpClient tcpClient, ILogger logger)
        {
            Id = id;
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _logger = logger;
        }

        public async Task HandleCommunicationAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        // Client disconnected
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    _logger.LogDebug("Received message from client {ClientId}: {Message}", Id, message);

                    // Process the message (you could add more complex processing here)
                    try
                    {
                        var socketMessage = JsonSerializer.Deserialize<SocketMessage>(message);

                        // Respond to ping messages
                        if (socketMessage?.Type == MessageType.Ping)
                        {
                            var response = new SocketMessage
                            {
                                Type = MessageType.Ping,
                                Data = "Pong",
                                Timestamp = DateTime.UtcNow
                            };

                            SendMessage(JsonSerializer.Serialize(response));
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse message from client {ClientId}", Id);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (IOException)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in client communication for {ClientId}", Id);
            }
        }

        public void SendMessage(string message)
        {
            if (!_tcpClient.Connected)
                throw new InvalidOperationException("Client is not connected");

            var data = Encoding.UTF8.GetBytes(message);
            _stream.Write(data, 0, data.Length);
            _stream.Flush();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _stream.Close();
                _tcpClient.Close();
                _tcpClient.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing client connection {ClientId}", Id);
            }

            _disposed = true;
        }
    }
}
