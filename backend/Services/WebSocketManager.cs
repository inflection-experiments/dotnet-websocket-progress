using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public class WebSocketManager : IWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
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

    public async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var socketId = Guid.NewGuid().ToString();
        _sockets.TryAdd(socketId, webSocket);
        _socketToId.TryAdd(webSocket, socketId); // Add reverse lookup
        _logger.LogInformation("WebSocket connected: {SocketId}. Total connections: {Count}", 
            socketId, _sockets.Count);

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
            _sockets.TryRemove(socketId, out _);
            _socketToId.TryRemove(webSocket, out _); // Remove reverse lookup
            _logger.LogInformation("WebSocket disconnected: {SocketId}. Total connections: {Count}", 
                socketId, _sockets.Count);
            
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
        if (_sockets.IsEmpty) return;

        var messageJson = JsonSerializer.Serialize(message, JsonOptions);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        
        var tasks = new List<Task>();
        
        foreach (var socket in _sockets.Values.ToList())
        {
            if (socket.State == WebSocketState.Open)
            {
                tasks.Add(SendBytesToSocketAsync(socket, messageBytes));
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
        if (_sockets.TryGetValue(clientId, out var webSocket) && webSocket.State == WebSocketState.Open)
        {
            await SendMessageToSocketAsync(webSocket, message);
        }
        else
        {
            _logger.LogWarning("Cannot send message to client {ClientId}: Socket not found or not open", clientId);
        }
    }

    public string? GetSocketIdForWebSocket(WebSocket webSocket)
    {
        return _socketToId.TryGetValue(webSocket, out var socketId) ? socketId : null;
    }

    public void RemoveSocket(WebSocket webSocket)
    {
        // Remove from reverse lookup first
        if (_socketToId.TryRemove(webSocket, out var socketId))
        {
            _sockets.TryRemove(socketId, out _);
        }
        else
        {
            // Fallback: find by value (less efficient but ensures cleanup)
            var socketToRemove = _sockets.FirstOrDefault(x => x.Value == webSocket);
            if (!socketToRemove.Equals(default(KeyValuePair<string, WebSocket>)))
            {
                _sockets.TryRemove(socketToRemove.Key, out _);
            }
        }
    }

    public int GetConnectionCount()
    {
        return _sockets.Count(x => x.Value.State == WebSocketState.Open);
    }
}