namespace WebSocketsBackend.Services;

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
                // Short delay before retrying to avoid tight loop on persistent errors
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Service is stopping");
        await base.StopAsync(stoppingToken);
    }
}