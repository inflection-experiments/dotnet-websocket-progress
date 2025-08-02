namespace WebSocketsBackend.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public int Progress { get; set; } = 0;
    public string ClientId { get; set; } = string.Empty; // Socket ID or Session ID of the client who triggered this task
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}