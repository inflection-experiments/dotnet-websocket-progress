namespace WebSocketsBackend.Models;

public class TaskRequest
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; } = 5000; // Duration in milliseconds for demo
    public Dictionary<string, object>? Parameters { get; set; }
}