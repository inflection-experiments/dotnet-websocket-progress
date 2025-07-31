using Microsoft.AspNetCore.Mvc;
using WebSocketsBackend.Models;
using WebSocketsBackend.Services;

namespace WebSocketsBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly ITaskQueue _taskQueue;
    private readonly IWebSocketManager _webSocketManager;
    private readonly ILogger<TaskController> _logger;

    public TaskController(
        ITaskQueue taskQueue,
        IWebSocketManager webSocketManager,
        ILogger<TaskController> logger)
    {
        _taskQueue = taskQueue;
        _webSocketManager = webSocketManager;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<ActionResult<TaskResponse>> StartTask([FromBody] TaskRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new TaskResponse
                {
                    Message = "Task name is required",
                    Status = "Error"
                });
            }

            var taskItem = new TaskItem
            {
                Name = request.Name,
                Status = "Queued",
                Parameters = new Dictionary<string, object>
                {
                    ["duration"] = request.Duration
                }
            };

            // Add any additional parameters from the request
            if (request.Parameters != null)
            {
                foreach (var param in request.Parameters)
                {
                    taskItem.Parameters[param.Key] = param.Value;
                }
            }

            // Queue the task
            await _taskQueue.QueueBackgroundWorkItemAsync(taskItem);

            _logger.LogInformation("Task {TaskId} queued: {TaskName}", taskItem.Id, taskItem.Name);

            // Notify WebSocket clients about the new task
            await _webSocketManager.BroadcastMessageAsync(new WebSocketMessage
            {
                Type = "taskQueued",
                Data = new
                {
                    id = taskItem.Id,
                    name = taskItem.Name,
                    status = taskItem.Status,
                    progress = taskItem.Progress,
                    createdAt = taskItem.CreatedAt
                }
            });

            // Return immediately with task ID
            return Ok(new TaskResponse
            {
                TaskId = taskItem.Id,
                Message = "Task has been queued successfully",
                Status = "Queued"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing task: {TaskName}", request.Name);
            return StatusCode(500, new TaskResponse
            {
                TaskId = "",
                Message = "Failed to queue task",
                Status = "Error"
            });
        }
    }

    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        try
        {
            var connectionCount = _webSocketManager.GetConnectionCount();
            var queuedTasks = _taskQueue.GetQueuedCount();
            
            return Ok(new 
            { 
                Message = "Task service is running", 
                Timestamp = DateTime.UtcNow,
                ActiveWebSocketConnections = connectionCount,
                QueuedTasks = queuedTasks,
                Status = "Healthy"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service status");
            return StatusCode(500, new
            {
                Message = "Error retrieving service status",
                Status = "Error",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpGet("health")]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "WebSockets Background Task API"
        });
    }
}