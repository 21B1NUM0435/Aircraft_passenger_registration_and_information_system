﻿using System;
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

        // Make clients field private and ClientConnection public
        private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();
        private Task? _serverTask;

        // Message queue for reliable delivery
        private readonly ConcurrentQueue<BroadcastMessage> _messageQueue = new();
        private Task? _messageProcessorTask;

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

            // Start message processor
            _messageProcessorTask = Task.Run(async () => await ProcessMessageQueueAsync(_cts.Token), _cts.Token);

            // Start accepting clients
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
            Console.WriteLine($"Broadcasting message to {_clients.Count} clients: {message}");

            // Add message to queue for reliable delivery
            _messageQueue.Enqueue(new BroadcastMessage
            {
                Content = message,
                Timestamp = DateTime.UtcNow,
                RetryCount = 0
            });
        }

        private async Task ProcessMessageQueueAsync(CancellationToken cancellationToken)
        {
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
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message queue");
                }
            }
        }

        private async Task DeliverBroadcastMessage(BroadcastMessage message)
        {
            var failedClients = new List<Guid>();
            var successCount = 0;

            foreach (var client in _clients.Values)
            {
                try
                {
                    client.SendMessage(message.Content);
                    successCount++;
                    Console.WriteLine($"Message delivered to client {client.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message to client {client.Id}: {ex.Message}");
                    failedClients.Add(client.Id);
                }
            }

            Console.WriteLine($"Message delivered to {successCount}/{_clients.Count} clients");

            // Clean up failed clients
            foreach (var clientId in failedClients)
            {
                if (_clients.TryRemove(clientId, out var client))
                {
                    client.Dispose();
                    Console.WriteLine($"Removed failed client {clientId}");
                }
            }

            // Retry logic for important messages
            if (failedClients.Any() && message.RetryCount < 3)
            {
                message.RetryCount++;
                Console.WriteLine($"Retrying message delivery (attempt {message.RetryCount})");
                await Task.Delay(1000);
                _messageQueue.Enqueue(message);
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync();
                    _logger.LogInformation("New client connected from {RemoteEndPoint}", tcpClient.Client.RemoteEndPoint);

                    var clientId = Guid.NewGuid();
                    var clientConnection = new ClientConnection(clientId, tcpClient, _logger, _clients);
                    _clients.TryAdd(clientId, clientConnection);

                    // Start a task to handle this client
                    _ = Task.Run(async () => await HandleClientAsync(clientConnection, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
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

        public int GetConnectedClientsCount()
        {
            return _clients.Count;
        }

        public List<ClientInfo> GetConnectedClients()
        {
            return _clients.Values.Select(c => new ClientInfo
            {
                Id = c.Id,
                ConnectedAt = c.ConnectedAt,
                RemoteEndPoint = c.RemoteEndPoint,
                SubscribedFlights = c.SubscribedFlights.ToList()
            }).ToList();
        }
    }

    // Make ClientConnection public to fix accessibility error
    public class ClientConnection : IDisposable
    {
        public Guid Id { get; }
        public DateTime ConnectedAt { get; }
        public string RemoteEndPoint { get; }
        public HashSet<string> SubscribedFlights { get; } = new();

        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, ClientConnection> _allClients;
        private bool _disposed = false;

        public ClientConnection(Guid id, TcpClient tcpClient, ILogger logger, ConcurrentDictionary<Guid, ClientConnection> allClients)
        {
            Id = id;
            ConnectedAt = DateTime.UtcNow;
            RemoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _logger = logger;
            _allClients = allClients;
        }

        public async Task HandleCommunicationAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                // Send welcome message (no await needed since it's void)
                SendWelcomeMessage();

                while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    _logger.LogDebug("Received message from client {ClientId}: {Message}", Id, message);

                    ProcessClientMessage(message); // No await needed
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

        private void ProcessClientMessage(string message)
        {
            try
            {
                Console.WriteLine($"Raw message received: {message}");

                var socketMessage = JsonSerializer.Deserialize<SocketMessage>(message);
                if (socketMessage == null) return;

                Console.WriteLine($"Parsed message type: {socketMessage.Type}");

                switch (socketMessage.Type)
                {
                    case MessageType.Ping:
                        SendPongResponse();
                        break;

                    case MessageType.FlightSubscription:
                        HandleFlightSubscription(socketMessage);
                        break;

                    case MessageType.SeatAssignment:
                    case MessageType.FlightStatusUpdate:
                    case MessageType.SeatLock:
                        // Forward to other clients
                        ForwardMessageToOtherClients(socketMessage);
                        break;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to parse message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private void ForwardMessageToOtherClients(SocketMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                Console.WriteLine($"Forwarding message type {message.Type} to other clients");

                var failedClients = new List<Guid>();

                // Send to all other clients (not this one)
                foreach (var client in _allClients.Values.Where(c => c.Id != this.Id))
                {
                    try
                    {
                        client.SendMessage(json);
                        Console.WriteLine($"Forwarded message to client {client.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to forward message to client {client.Id}: {ex.Message}");
                        failedClients.Add(client.Id);
                    }
                }

                // Clean up failed clients
                foreach (var clientId in failedClients)
                {
                    if (_allClients.TryRemove(clientId, out var client))
                    {
                        client.Dispose();
                        Console.WriteLine($"Removed failed client {clientId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error forwarding message: {ex.Message}");
            }
        }

        private void HandleFlightSubscription(SocketMessage message)
        {
            try
            {
                if (message.Data is JsonElement element && element.TryGetProperty("flightNumber", out var flightNumberProp))
                {
                    var flightNumber = flightNumberProp.GetString();
                    if (!string.IsNullOrEmpty(flightNumber))
                    {
                        SubscribedFlights.Add(flightNumber);
                        _logger.LogInformation("Client {ClientId} subscribed to flight {FlightNumber}", Id, flightNumber);

                        // Send confirmation
                        var response = new SocketMessage
                        {
                            Type = MessageType.System,
                            Data = new { success = true, flightNumber = flightNumber },
                            Timestamp = DateTime.UtcNow
                        };

                        SendMessage(JsonSerializer.Serialize(response));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling flight subscription for client {ClientId}", Id);
            }
        }

        private void SendWelcomeMessage()
        {
            var welcomeMessage = new SocketMessage
            {
                Type = MessageType.System,
                Data = new { message = "Connected to Flight Management Socket Server", clientId = Id },
                Timestamp = DateTime.UtcNow
            };

            SendMessage(JsonSerializer.Serialize(welcomeMessage));
        }

        private void SendPongResponse()
        {
            var response = new SocketMessage
            {
                Type = MessageType.Ping,
                Data = "Pong",
                Timestamp = DateTime.UtcNow
            };

            SendMessage(JsonSerializer.Serialize(response));
        }

        public void SendMessage(string message)
        {
            if (!_tcpClient.Connected || _disposed)
                return;

            try
            {
                var data = Encoding.UTF8.GetBytes(message);
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to client {ClientId}", Id);
                throw;
            }
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

    // Supporting classes
    public class BroadcastMessage
    {
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int RetryCount { get; set; }
    }

    public class ClientInfo
    {
        public Guid Id { get; set; }
        public DateTime ConnectedAt { get; set; }
        public string RemoteEndPoint { get; set; } = string.Empty;
        public List<string> SubscribedFlights { get; set; } = new();
    }
}