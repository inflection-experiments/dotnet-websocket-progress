using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public interface ITaskProcessor
{
    Task ProcessTaskAsync(TaskItem taskItem, CancellationToken cancellationToken);
}