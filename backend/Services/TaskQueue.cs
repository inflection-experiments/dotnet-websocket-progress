using System.Threading.Channels;
using WebSocketsBackend.Models;

namespace WebSocketsBackend.Services;

public class TaskQueue : ITaskQueue
{
    private readonly Channel<TaskItem> _queue;
    private readonly ILogger<TaskQueue> _logger;

    public TaskQueue(ILogger<TaskQueue> logger)
    {
        _logger = logger;
        
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
        _logger.LogDebug("Task {TaskId} queued: {TaskName}", workItem.Id, workItem.Name);
    }

    public async ValueTask<TaskItem?> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }

    public int GetQueuedCount()
    {
        return _queue.Reader.Count;
    }
}