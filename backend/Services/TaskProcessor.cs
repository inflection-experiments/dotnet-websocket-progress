using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public class TaskProcessor : ITaskProcessor
{
    private readonly IWebSocketManager _webSocketManager;
    private readonly ILogger<TaskProcessor> _logger;

    public TaskProcessor(IWebSocketManager webSocketManager, ILogger<TaskProcessor> logger)
    {
        _webSocketManager = webSocketManager;
        _logger = logger;
    }

    public async Task ProcessTaskAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting task {TaskId}: {TaskName}", taskItem.Id, taskItem.Name);
            
            taskItem.StartedAt = DateTime.UtcNow;

            // Update status to running
            taskItem.Status = "Running";
            taskItem.Progress = 0;
            await NotifyProgress(taskItem);

            // Simulate work with progress updates
            var totalSteps = 10;
            for (int i = 1; i <= totalSteps; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    taskItem.Status = "Cancelled";
                    await NotifyProgress(taskItem);
                    return;
                }

                // Simulate some work (configurable duration from request)
                var stepDelay = taskItem.Parameters?.ContainsKey("duration") == true 
                    ? (int)taskItem.Parameters["duration"] / totalSteps 
                    : 500;
                    
                await Task.Delay(stepDelay, cancellationToken);

                // Update progress
                taskItem.Progress = (i * 100) / totalSteps;
                taskItem.Status = $"Processing step {i}/{totalSteps}";
                await NotifyProgress(taskItem);

                _logger.LogDebug("Task {TaskId} progress: {Progress}%", taskItem.Id, taskItem.Progress);
            }

            // Complete the task
            taskItem.Status = "Completed";
            taskItem.Progress = 100;
            taskItem.CompletedAt = DateTime.UtcNow;
            taskItem.Result = $"Task '{taskItem.Name}' completed successfully at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
            await NotifyProgress(taskItem);

            _logger.LogInformation("Task {TaskId} completed successfully", taskItem.Id);
        }
        catch (OperationCanceledException)
        {
            taskItem.Status = "Cancelled";
            taskItem.CompletedAt = DateTime.UtcNow;
            await NotifyProgress(taskItem);
            _logger.LogInformation("Task {TaskId} was cancelled", taskItem.Id);
        }
        catch (Exception ex)
        {
            taskItem.Status = "Failed";
            taskItem.Error = ex.Message;
            taskItem.CompletedAt = DateTime.UtcNow;
            await NotifyProgress(taskItem);
            _logger.LogError(ex, "Task {TaskId} failed", taskItem.Id);
        }
    }

    private async Task NotifyProgress(TaskItem taskItem)
    {
        var message = new WebSocketMessage
        {
            Type = "taskProgress",
            Data = new
            {
                id = taskItem.Id,
                name = taskItem.Name,
                status = taskItem.Status,
                progress = taskItem.Progress,
                result = taskItem.Result,
                error = taskItem.Error,
                createdAt = taskItem.CreatedAt,
                startedAt = taskItem.StartedAt,
                completedAt = taskItem.CompletedAt,
                clientId = taskItem.ClientId
            }
        };

        // Send progress update only to the specific client who triggered this task
        if (!string.IsNullOrEmpty(taskItem.ClientId))
        {
            await _webSocketManager.SendMessageToClientAsync(taskItem.ClientId, message);
        }
        else
        {
            // Fallback: broadcast if no client ID is set (backwards compatibility)
            _logger.LogWarning("Task {TaskId} has no ClientId, broadcasting to all clients", taskItem.Id);
            await _webSocketManager.BroadcastMessageAsync(message);
        }
    }
}