using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public interface ITaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(TaskItem workItem);
    ValueTask<TaskItem?> DequeueAsync(CancellationToken cancellationToken);
    int GetQueuedCount();
}