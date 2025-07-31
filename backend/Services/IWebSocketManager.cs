using System.Net.WebSockets;
using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public interface IWebSocketManager
{
    Task HandleWebSocketAsync(WebSocket webSocket);
    Task BroadcastMessageAsync(WebSocketMessage message);
    Task SendMessageToSocketAsync(WebSocket webSocket, WebSocketMessage message);
    void RemoveSocket(WebSocket webSocket);
    int GetConnectionCount();
}