using System.Net.WebSockets;
using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public interface IWebSocketManager
{
    Task HandleWebSocketAsync(WebSocket webSocket);
    Task BroadcastMessageAsync(WebSocketMessage message);
    Task SendMessageToSocketAsync(WebSocket webSocket, WebSocketMessage message);
    Task SendMessageToClientAsync(string clientId, WebSocketMessage message); // Send message to specific client by ID
    string? GetSocketIdForWebSocket(WebSocket webSocket); // Get socket ID for a specific WebSocket
    void RemoveSocket(WebSocket webSocket);
    int GetConnectionCount();
}