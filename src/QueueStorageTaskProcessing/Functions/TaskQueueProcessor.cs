using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueStorageTaskProcessing.Models;

namespace QueueStorageTaskProcessing.Functions;

/// <summary>
/// Processes task messages from Azure Queue Storage.
///
/// Key concepts demonstrated:
/// • Base64 decoding — the Azure Functions Queue trigger automatically Base64-decodes
///   the raw message body before passing it to the function.
/// • At-least-once delivery — a message becomes visible again if processing fails
///   (the function throws) or the visibility timeout expires before deletion.
/// • Dequeue count tracking — <c>myQueueItem.DequeueCount</c> increments on each
///   re-delivery; use it to apply different retry strategies (e.g. skip heavy work
///   on the first quick attempt, escalate alerting as retries increase).
/// • Poison queue — after <c>maxDequeueCount</c> failures (configured in host.json),
///   the runtime moves the message to <c>&lt;queueName&gt;-poison</c> automatically.
/// </summary>
public class TaskQueueProcessor
{
    private readonly ILogger<TaskQueueProcessor> _logger;

    // Threshold at which we log an elevated warning before the poison queue takes over.
    // Must be less than host.json maxDequeueCount (default 5).
    private const int RetryWarningThreshold = 3;

    public TaskQueueProcessor(ILogger<TaskQueueProcessor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(TaskQueueProcessor))]
    public async Task Run(
        // The trigger decodes the Base64 message and deserialises the JSON body automatically.
        [QueueTrigger("%TaskQueueName%", Connection = "AzureWebJobsStorage")]
        TaskMessage taskMessage,
        FunctionContext context)
    {
        // DequeueCount is available via FunctionContext metadata; the binding also exposes
        // it as a property on QueueMessage when using the raw-string overload.
        var dequeueCount = GetDequeueCount(context);

        _logger.LogInformation(
            "Processing task {TaskId} (type: {TaskType}, attempt: {Attempt})",
            taskMessage.TaskId, taskMessage.TaskType, dequeueCount);

        if (dequeueCount >= RetryWarningThreshold)
        {
            _logger.LogWarning(
                "Task {TaskId} has been attempted {DequeueCount} time(s). " +
                "It will be moved to the poison queue after {MaxDequeueCount} attempts.",
                taskMessage.TaskId, dequeueCount, 5);
        }

        var started = DateTimeOffset.UtcNow;
        await ProcessTaskAsync(taskMessage, dequeueCount);

        _logger.LogInformation(
            "Task {TaskId} completed in {Elapsed:N0} ms",
            taskMessage.TaskId,
            (DateTimeOffset.UtcNow - started).TotalMilliseconds);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private async Task ProcessTaskAsync(TaskMessage message, int dequeueCount)
    {
        // Dispatch to the appropriate handler based on task type.
        switch (message.TaskType)
        {
            case "SendEmail":
                await HandleSendEmailAsync(message);
                break;
            case "GenerateReport":
                await HandleGenerateReportAsync(message);
                break;
            default:
                _logger.LogWarning(
                    "Unknown task type '{TaskType}' for task {TaskId}. Message will be retried.",
                    message.TaskType, message.TaskId);
                // Throw to trigger a retry so the message is not silently dropped.
                throw new NotSupportedException($"Task type '{message.TaskType}' is not supported.");
        }
    }

    private async Task HandleSendEmailAsync(TaskMessage message)
    {
        _logger.LogInformation("Sending email for task {TaskId}: {Payload}", message.TaskId, message.Payload);
        // Simulate email sending.
        await Task.Delay(TimeSpan.FromMilliseconds(50));
    }

    private async Task HandleGenerateReportAsync(TaskMessage message)
    {
        _logger.LogInformation("Generating report for task {TaskId}: {Payload}", message.TaskId, message.Payload);
        // Simulate report generation.
        await Task.Delay(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Reads the queue dequeue count from <see cref="FunctionContext"/> trigger metadata.
    /// Returns 1 when the metadata is unavailable (e.g. unit tests).
    /// </summary>
    private static int GetDequeueCount(FunctionContext context)
    {
        if (context.BindingContext.BindingData.TryGetValue("DequeueCount", out var raw)
            && int.TryParse(raw?.ToString(), out var count))
        {
            return count;
        }

        return 1;
    }
}
