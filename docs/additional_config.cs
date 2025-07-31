// appsettings.json - Configuration for production
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "BackgroundTaskService": "Information",
      "WebSocketManager": "Debug"
    }
  },
  "TaskSettings": {
    "MaxConcurrentTasks": 10,
    "QueueCapacity": 1000,
    "TaskTimeout": 300000
  },
  "WebSocketSettings": {
    "KeepAliveInterval": 30000,
    "MaxConnections": 1000,
    "BufferSize": 4096
  }
}

// Enhanced Program.cs with production configuration
using System.Net.WebSockets;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Configure CORS for production
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://yourdomain.com") // Replace with your domain
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register our services with configuration
builder.Services.Configure<TaskSettings>(builder.Configuration.GetSection("TaskSettings"));
builder.Services.Configure<WebSocketSettings>(builder.Configuration.GetSection("WebSocketSettings"));
builder.Services.AddSingleton<ITaskQueue, TaskQueue>();
builder.Services.AddSingleton<IWebSocketManager, WebSocketManager>();
builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddScoped<ITaskProcessor, TaskProcessor>();

// Add memory cache for task status tracking
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITaskStatusService, TaskStatusService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Configure WebSockets
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    ReceiveBufferSize = 4096
});

app.UseRouting();
app.UseCors(); // Enable CORS
app.MapControllers();

// WebSocket endpoint with enhanced error handling
app.Map("/ws", async (HttpContext context, IWebSocketManager wsManager, ILogger<Program> logger) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        try
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await wsManager.HandleWebSocketAsync(webSocket);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling WebSocket connection");
            context.Response.StatusCode = 500;
        }
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

// Serve static files for the HTML client
app.UseStaticFiles();

// Health check endpoint
app.MapGet("/health", (IWebSocketManager wsManager) => 
{
    return Results.Ok(new 
    { 
        Status = "Healthy",
        Timestamp = DateTime.UtcNow,
        ActiveConnections = wsManager.GetConnectionCount()
    });
});

app.Run();

// Configuration models
public class TaskSettings
{
    public int MaxConcurrentTasks { get; set; } = 10;
    public int QueueCapacity { get; set; } = 1000;
    public int TaskTimeout { get; set; } = 300000; // 5 minutes
}

public class WebSocketSettings
{
    public int KeepAliveInterval { get; set; } = 30000; // 30 seconds
    public int MaxConnections { get; set; } = 1000;
    public int BufferSize { get; set; } = 4096;
}

// Enhanced WebSocket Manager with connection limits and monitoring
public class EnhancedWebSocketManager : IWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
    private readonly ILogger<EnhancedWebSocketManager> _logger;
    private readonly WebSocketSettings _settings;
    private readonly Timer _keepAliveTimer;

    public EnhancedWebSocketManager(
        ILogger<EnhancedWebSocketManager> logger, 
        IOptions<WebSocketSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        
        // Set up keep-alive timer
        _keepAliveTimer = new Timer(SendKeepAlive, null, 
            TimeSpan.FromMilliseconds(_settings.KeepAliveInterval),
            TimeSpan.FromMilliseconds(_settings.KeepAliveInterval));
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        // Check connection limit
        if (_connections.Count >= _settings.MaxConnections)
        {
            _logger.LogWarning("Connection limit reached. Rejecting new connection.");
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, 
                "Connection limit reached", CancellationToken.None);
            return;
        }

        var socketId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection
        {
            Id = socketId,
            Socket = webSocket,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };

        _connections.TryAdd(socketId, connection);
        _logger.LogInformation("WebSocket connected: {SocketId}. Total connections: {Count}", 
            socketId, _connections.Count);

        try
        {
            // Send welcome message
            await SendMessageToSocketAsync(webSocket, new WebSocketMessage
            {
                Type = "connection",
                Data = new { status = "connected", socketId, serverTime = DateTime.UtcNow }
            });

            // Handle connection
            await HandleWebSocketConnection(connection);
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
            _connections.TryRemove(socketId, out _);
            _logger.LogInformation("WebSocket disconnected: {SocketId}. Total connections: {Count}", 
                socketId, _connections.Count);
            
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                    "Connection closed", CancellationToken.None);
            }
        }
    }

    private async Task HandleWebSocketConnection(WebSocketConnection connection)
    {
        var buffer = new byte[_settings.BufferSize];
        
        while (connection.Socket.State == WebSocketState.Open)
        {
            var result = await connection.Socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            
            connection.LastActivity = DateTime.UtcNow;
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("Received message from {SocketId}: {Message}", 
                    connection.Id, message);
                
                await HandleIncomingMessage(connection, message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }

    private async Task HandleIncomingMessage(WebSocketConnection connection, string message)
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
                        await SendMessageToSocketAsync(connection.Socket, new WebSocketMessage
                        {
                            Type = "pong",
                            Data = new { timestamp = DateTime.UtcNow, socketId = connection.Id }
                        });
                        break;
                    
                    case "subscribe":
                        // Handle subscription to specific task updates
                        if (messageObj.TryGetProperty("taskId", out var taskIdElement))
                        {
                            var taskId = taskIdElement.GetString();
                            connection.SubscribedTasks.Add(taskId);
                            _logger.LogDebug("Socket {SocketId} subscribed to task {TaskId}", 
                                connection.Id, taskId);
                        }
                        break;
                    
                    case "unsubscribe":
                        // Handle unsubscription
                        if (messageObj.TryGetProperty("taskId", out var unsubTaskIdElement))
                        {
                            var taskId = unsubTaskIdElement.GetString();
                            connection.SubscribedTasks.Remove(taskId);
                            _logger.LogDebug("Socket {SocketId} unsubscribed from task {TaskId}", 
                                connection.Id, taskId);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling incoming message from {SocketId}: {Message}", 
                connection.Id, message);
        }
    }

    private async void SendKeepAlive(object? state)
    {
        var keepAliveMessage = new WebSocketMessage
        {
            Type = "keepAlive",
            Data = new { timestamp = DateTime.UtcNow }
        };

        var staleConnections = new List<string>();
        var tasks = new List<Task>();

        foreach (var kvp in _connections)
        {
            var connection = kvp.Value;
            
            // Check for stale connections (no activity for 5 minutes)
            if (DateTime.UtcNow - connection.LastActivity > TimeSpan.FromMinutes(5))
            {
                staleConnections.Add(kvp.Key);
                continue;
            }

            if (connection.Socket.State == WebSocketState.Open)
            {
                tasks.Add(SendMessageToSocketAsync(connection.Socket, keepAliveMessage));
            }
        }

        // Remove stale connections
        foreach (var staleId in staleConnections)
        {
            if (_connections.TryRemove(staleId, out var staleConnection))
            {
                _logger.LogInformation("Removing stale connection: {SocketId}", staleId);
                try
                {
                    await staleConnection.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                        "Stale connection", CancellationToken.None);
                }
                catch { /* Ignore errors when closing stale connections */ }
            }
        }

        if (tasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending keep-alive messages");
            }
        }
    }

    public async Task BroadcastMessageAsync(WebSocketMessage message)
    {
        if (_connections.IsEmpty) return;

        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        
        var tasks = new List<Task>();
        
        foreach (var connection in _connections.Values.ToList())
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                tasks.Add(SendBytesToSocketAsync(connection.Socket, messageBytes));
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
            var messageJson = JsonSerializer.Serialize(message);
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

    public void RemoveSocket(WebSocket webSocket)
    {
        var connectionToRemove = _connections.FirstOrDefault(x => x.Value.Socket == webSocket);
        if (!connectionToRemove.Equals(default(KeyValuePair<string, WebSocketConnection>)))
        {
            _connections.TryRemove(connectionToRemove.Key, out _);
        }
    }

    public int GetConnectionCount()
    {
        return _connections.Count(x => x.Value.Socket.State == WebSocketState.Open);
    }
}

// WebSocket Connection Model
public class WebSocketConnection
{
    public string Id { get; set; } = string.Empty;
    public WebSocket Socket { get; set; } = null!;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public HashSet<string> SubscribedTasks { get; set; } = new();
}

// Task Status Service (same as before but mentioned for completeness)
public interface ITaskStatusService
{
    void UpdateTaskStatus(string taskId, TaskItem taskItem);
    TaskItem? GetTaskStatus(string taskId);
    IEnumerable<TaskItem> GetAllTasks();
    void RemoveTask(string taskId);
}

public class TaskStatusService : ITaskStatusService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TaskStatusService> _logger;

    public TaskStatusService(IMemoryCache cache, ILogger<TaskStatusService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void UpdateTaskStatus(string taskId, TaskItem taskItem)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            SlidingExpiration = TimeSpan.FromHours(1)
        };

        _cache.Set($"task_{taskId}", taskItem, cacheOptions);
        _logger.LogDebug("Updated task status for {TaskId}: {Status}", taskId, taskItem.Status);
    }

    public TaskItem? GetTaskStatus(string taskId)
    {
        return _cache.Get<TaskItem>($"task_{taskId}");
    }

    public IEnumerable<TaskItem> GetAllTasks()
    {
        // This is a simplified implementation
        // In production, you might want to use a proper data store
        return new List<TaskItem>();
    }

    public void RemoveTask(string taskId)
    {
        _cache.Remove($"task_{taskId}");
        _logger.LogDebug("Removed task {TaskId} from cache", taskId);
    }
}