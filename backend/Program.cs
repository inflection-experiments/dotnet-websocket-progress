using System.Net.WebSockets;
using WebSocketsBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Configure CORS for the SvelteKit frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:4173") // SvelteKit dev and preview ports
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register our services
builder.Services.AddSingleton<ITaskQueue, TaskQueue>();
builder.Services.AddSingleton<IWebSocketManager, WebSocketsBackend.Services.WebSocketManager>();
builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddScoped<ITaskProcessor, TaskProcessor>();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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

// WebSocket endpoint
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

// Health check endpoint
app.MapGet("/health", (IWebSocketManager wsManager) => 
{
    return Results.Ok(new 
    { 
        Status = "Healthy",
        Timestamp = DateTime.UtcNow,
        ActiveConnections = wsManager.GetConnectionCount(),
        Service = "WebSockets Background Task API"
    });
});

// Default endpoint
app.MapGet("/", () => 
{
    return Results.Json(new
    {
        Message = "WebSockets Background Task API",
        Version = "1.0.0",
        Timestamp = DateTime.UtcNow,
        Endpoints = new
        {
            Health = "/health",
            TaskStart = "/api/task/start",
            TaskStatus = "/api/task/status",
            WebSocket = "/ws"
        }
    });
});

app.Run();