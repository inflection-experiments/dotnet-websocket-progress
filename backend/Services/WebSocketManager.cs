using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

/// <summary>
/// Thread-safe wrapper around a list of WebSockets
/// </summary>
public class ConcurrentWebSocketList
{
    private readonly List<WebSocket> _sockets = new();
    private readonly object _lock = new();

    public void Add(WebSocket socket)
    {
        lock (_lock)
        {
            _sockets.Add(socket);
        }
    }

    public bool Remove(WebSocket socket)
    {
        lock (_lock)
        {
            return _sockets.Remove(socket);
        }
    }

    public List<WebSocket> GetSnapshot()
    {
        lock (_lock)
        {
            return new List<WebSocket>(_sockets);
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _sockets.Count == 0;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _sockets.Count;
            }
        }
    }
}

public class WebSocketManager : IWebSocketManager
{
    // Maps client/session id -> list of sockets
    private readonly ConcurrentDictionary<string, ConcurrentWebSocketList> _clientIdToSockets = new();
    private readonly ConcurrentDictionary<WebSocket, string> _socketToId = new(); // Reverse lookup from socket to ID
    private readonly ILogger<WebSocketManager> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WebSocketManager(ILogger<WebSocketManager> logger)
    {
        _logger = logger;
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket, string? desiredSocketId = null)
    {
        // Prefer provided desiredSocketId (e.g., sessionId). Multiple sockets can map to the same id.
        var socketId = !string.IsNullOrWhiteSpace(desiredSocketId) ? desiredSocketId : Guid.NewGuid().ToString();

        var socketList = _clientIdToSockets.GetOrAdd(socketId, static _ => new ConcurrentWebSocketList());
        socketList.Add(webSocket);
        _socketToId.TryAdd(webSocket, socketId);

        _logger.LogInformation("WebSocket connected: {SocketId}. Total open sockets: {Count}", 
            socketId, GetConnectionCount());

        try
        {
            // Send welcome message
            await SendMessageToSocketAsync(webSocket, new WebSocketMessage
            {
                Type = "connection",
                Data = new { status = "connected", socketId, serverTime = DateTime.UtcNow }
            });

            await HandleWebSocketConnection(webSocket);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error for {SocketId}", socketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling WebSocket {SocketId}", socketId);
        }
        finally
        {
            // Remove this socket from its client list
            if (_socketToId.TryRemove(webSocket, out var mappedId))
            {
                if (_clientIdToSockets.TryGetValue(mappedId, out var list))
                {
                    list.Remove(webSocket);
                    if (list.IsEmpty)
                    {
                        _clientIdToSockets.TryRemove(mappedId, out _);
                    }
                }
            }
            _logger.LogInformation("WebSocket disconnected: {SocketId}. Total open sockets: {Count}", 
                socketId, GetConnectionCount());
            
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                    "Connection closed", CancellationToken.None);
            }
        }
    }

    private async Task HandleWebSocketConnection(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("Received message: {Message}", message);
                
                await HandleIncomingMessage(webSocket, message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }

    private async Task HandleIncomingMessage(WebSocket webSocket, string message)
    {
        try
        {
            var messageObj = JsonSerializer.Deserialize<JsonElement>(message);
            
            if (messageObj.TryGetProperty("type", out var typeElement))
            {
                var messageType = typeElement.GetString();
                
                switch (messageType)
                {
                    case "ping":
                        await SendMessageToSocketAsync(webSocket, new WebSocketMessage
                        {
                            Type = "pong",
                            Data = new { timestamp = DateTime.UtcNow }
                        });
                        break;
                    default:
                        _logger.LogDebug("Unknown message type: {MessageType}", messageType);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling incoming message: {Message}", message);
        }
    }

    public async Task BroadcastMessageAsync(WebSocketMessage message)
    {
        if (_clientIdToSockets.IsEmpty) return;

        var messageJson = JsonSerializer.Serialize(message, JsonOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        
        var tasks = new List<Task>();
        
        foreach (var list in _clientIdToSockets.Values.ToList())
        {
            foreach (var socket in list.GetSnapshot())
            {
                if (socket.State == WebSocketState.Open)
                {
                    tasks.Add(SendBytesToSocketAsync(socket, messageBytes));
                }
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Broadcasted message to {Count} clients", tasks.Count);
        }
    }

    public async Task SendMessageToSocketAsync(WebSocket webSocket, WebSocketMessage message)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            var messageJson = JsonSerializer.Serialize(message, JsonOptions);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            await SendBytesToSocketAsync(webSocket, messageBytes);
        }
    }

    private async Task SendBytesToSocketAsync(WebSocket webSocket, byte[] messageBytes)
    {
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(messageBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Failed to send message to WebSocket");
            RemoveSocket(webSocket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending message to WebSocket");
            RemoveSocket(webSocket);
        }
    }

    public async Task SendMessageToClientAsync(string clientId, WebSocketMessage message)
    {
        if (_clientIdToSockets.TryGetValue(clientId, out var list) && !list.IsEmpty)
        {
            var tasks = new List<Task>();
            foreach (var socket in list.GetSnapshot())
            {
                if (socket.State == WebSocketState.Open)
                {
                    tasks.Add(SendMessageToSocketAsync(socket, message));
                }
            }
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
                return;
            }
        }
        _logger.LogWarning("Cannot send message to client {ClientId}: No open sockets", clientId);
    }

    public string? GetSocketIdForWebSocket(WebSocket webSocket)
    {
        return _socketToId.TryGetValue(webSocket, out var socketId) ? socketId : null;
    }

    public void RemoveSocket(WebSocket webSocket)
    {
        // Remove from reverse lookup then from its client list
        if (_socketToId.TryRemove(webSocket, out var clientId))
        {
            if (_clientIdToSockets.TryGetValue(clientId, out var list))
            {
                list.Remove(webSocket);
                if (list.IsEmpty)
                {
                    _clientIdToSockets.TryRemove(clientId, out _);
                }
            }
        }
    }

    public int GetConnectionCount()
    {
        var total = 0;
        foreach (var list in _clientIdToSockets.Values)
        {
            total += list.GetSnapshot().Count(s => s.State == WebSocketState.Open);
        }
        return total;
    }
}