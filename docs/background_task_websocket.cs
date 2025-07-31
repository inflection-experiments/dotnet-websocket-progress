// Program.cs
using System.Net.WebSockets;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Register our services
builder.Services.AddSingleton<ITaskQueue, TaskQueue>();
builder.Services.AddSingleton<IWebSocketManager, WebSocketManager>();
builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddScoped<ITaskProcessor, TaskProcessor>();

var app = builder.Build();

// Configure pipeline
app.UseWebSockets();
app.UseRouting();
app.MapControllers();

// WebSocket endpoint
app.Map("/ws", async (HttpContext context, IWebSocketManager wsManager) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wsManager.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Serve static files for the HTML client
app.UseStaticFiles();

app.Run();

// Models/TaskItem.cs
public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public int Progress { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Result { get; set; }
    public string? Error { get; set; }
}

public class TaskRequest
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; } = 5000; // Duration in milliseconds for demo
    public Dictionary<string, object>? Parameters { get; set; }
}

public class TaskResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class WebSocketMessage
{
    public string Type { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Services/ITaskQueue.cs
public interface ITaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(TaskItem workItem);
    ValueTask<TaskItem?> DequeueAsync(CancellationToken cancellationToken);
}

// Services/TaskQueue.cs
public class TaskQueue : ITaskQueue
{
    private readonly Channel<TaskItem> _queue;

    public TaskQueue()
    {
        // Create a channel with bounded capacity
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _queue = Channel.CreateBounded<TaskItem>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(TaskItem workItem)
    {
        if (workItem == null)
            throw new ArgumentNullException(nameof(workItem));

        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<TaskItem?> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}

// Services/IWebSocketManager.cs
public interface IWebSocketManager
{
    Task HandleWebSocketAsync(WebSocket webSocket);
    Task BroadcastMessageAsync(WebSocketMessage message);
    Task SendMessageToSocketAsync(WebSocket webSocket, WebSocketMessage message);
    void RemoveSocket(WebSocket webSocket);
    int GetConnectionCount();
}

// Services/WebSocketManager.cs
public class WebSocketManager : IWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ILogger<WebSocketManager> _logger;

    public WebSocketManager(ILogger<WebSocketManager> logger)
    {
        _logger = logger;
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var socketId = Guid.NewGuid().ToString();
        _sockets.TryAdd(socketId, webSocket);
        _logger.LogInformation("WebSocket connected: {SocketId}", socketId);

        try
        {
            // Send welcome message
            await SendMessageToSocketAsync(webSocket, new WebSocketMessage
            {
                Type = "connection",
                Data = new { status = "connected", socketId }
            });

            // Keep connection alive and handle incoming messages
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogDebug("Received message from {SocketId}: {Message}", socketId, message);
                    
                    // Handle incoming messages (ping/pong, etc.)
                    await HandleIncomingMessage(webSocket, message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
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
            _logger.LogInformation("WebSocket disconnected: {SocketId}", socketId);
            
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }
    }

    private async Task HandleIncomingMessage(WebSocket webSocket, string message)
    {
        try
        {
            // Parse incoming message and handle accordingly
            if (message.Contains("ping"))
            {
                await SendMessageToSocketAsync(webSocket, new WebSocketMessage
                {
                    Type = "pong",
                    Data = new { timestamp = DateTime.UtcNow }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling incoming message: {Message}", message);
        }
    }

    public async Task BroadcastMessageAsync(WebSocketMessage message)
    {
        var messageJson = System.Text.Json.JsonSerializer.Serialize(message);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
        
        var tasks = new List<Task>();
        
        foreach (var socket in _sockets.Values.ToList())
        {
            if (socket.State == WebSocketState.Open)
            {
                tasks.Add(SendBytesToSocketAsync(socket, messageBytes));
            }
        }

        await Task.WhenAll(tasks);
        _logger.LogDebug("Broadcasted message to {Count} clients", tasks.Count);
    }

    public async Task SendMessageToSocketAsync(WebSocket webSocket, WebSocketMessage message)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            var messageJson = System.Text.Json.JsonSerializer.Serialize(message);
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
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

    public void RemoveSocket(WebSocket webSocket)
    {
        var socketToRemove = _sockets.FirstOrDefault(x => x.Value == webSocket);
        if (!socketToRemove.Equals(default(KeyValuePair<string, WebSocket>)))
        {
            _sockets.TryRemove(socketToRemove.Key, out _);
        }
    }

    public int GetConnectionCount()
    {
        return _sockets.Count(x => x.Value.State == WebSocketState.Open);
    }
}

// Services/ITaskProcessor.cs
public interface ITaskProcessor
{
    Task ProcessTaskAsync(TaskItem taskItem, CancellationToken cancellationToken);
}

// Services/TaskProcessor.cs
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

                // Simulate some work
                await Task.Delay(500, cancellationToken);

                // Update progress
                taskItem.Progress = (i * 100) / totalSteps;
                taskItem.Status = $"Processing step {i}/{totalSteps}";
                await NotifyProgress(taskItem);

                _logger.LogDebug("Task {TaskId} progress: {Progress}%", taskItem.Id, taskItem.Progress);
            }

            // Complete the task
            taskItem.Status = "Completed";
            taskItem.Progress = 100;
            taskItem.Result = $"Task '{taskItem.Name}' completed successfully at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
            await NotifyProgress(taskItem);

            _logger.LogInformation("Task {TaskId} completed successfully", taskItem.Id);
        }
        catch (OperationCanceledException)
        {
            taskItem.Status = "Cancelled";
            await NotifyProgress(taskItem);
            _logger.LogInformation("Task {TaskId} was cancelled", taskItem.Id);
        }
        catch (Exception ex)
        {
            taskItem.Status = "Failed";
            taskItem.Error = ex.Message;
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
                taskItem.Id,
                taskItem.Name,
                taskItem.Status,
                taskItem.Progress,
                taskItem.Result,
                taskItem.Error
            }
        };

        await _webSocketManager.BroadcastMessageAsync(message);
    }
}

// Services/BackgroundTaskService.cs
public class BackgroundTaskService : BackgroundService
{
    private readonly ITaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskService> _logger;

    public BackgroundTaskService(
        ITaskQueue taskQueue,
        IServiceProvider serviceProvider,
        ILogger<BackgroundTaskService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Service started");

        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                if (workItem != null)
                {
                    // Process the task in the background without waiting
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var taskProcessor = scope.ServiceProvider.GetRequiredService<ITaskProcessor>();
                        await taskProcessor.ProcessTaskAsync(workItem, stoppingToken);
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing background work item");
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Service is stopping");
        await base.StopAsync(stoppingToken);
    }
}

// Controllers/TaskController.cs
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
            var taskItem = new TaskItem
            {
                Name = request.Name,
                Status = "Queued"
            };

            // Queue the task
            await _taskQueue.QueueBackgroundWorkItemAsync(taskItem);

            _logger.LogInformation("Task {TaskId} queued: {TaskName}", taskItem.Id, taskItem.Name);

            // Notify WebSocket clients about the new task
            await _webSocketManager.BroadcastMessageAsync(new WebSocketMessage
            {
                Type = "taskQueued",
                Data = new
                {
                    taskItem.Id,
                    taskItem.Name,
                    taskItem.Status,
                    taskItem.Progress
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
            _logger.LogError(ex, "Error queuing task");
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
        var connectionCount = _webSocketManager.GetConnectionCount();
        return Ok(new 
        { 
            Message = "Task service is running", 
            Timestamp = DateTime.UtcNow,
            ActiveWebSocketConnections = connectionCount
        });
    }
}