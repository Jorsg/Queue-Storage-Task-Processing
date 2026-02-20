using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueStorageTaskProcessing.Models;
using QueueStorageTaskProcessing.Services;

namespace QueueStorageTaskProcessing.Functions;

/// <summary>
/// Handles messages that have been moved to the poison queue after exhausting retries.
///
/// Key concepts demonstrated:
/// • Poison queue naming convention — Azure Functions appends <c>-poison</c> to the
///   source queue name (e.g. <c>task-queue</c> → <c>task-queue-poison</c>).
/// • When a message's dequeue count exceeds <c>maxDequeueCount</c> in host.json the
///   runtime moves it here automatically; processing never succeeds in the source queue.
/// • Strategies: log + alert, store for manual review, dead-letter to another system,
///   or route to a compensating workflow.
/// </summary>
public class PoisonMessageProcessor
{
    private readonly IQueueService _queueService;
    private readonly ILogger<PoisonMessageProcessor> _logger;

    public PoisonMessageProcessor(IQueueService queueService, ILogger<PoisonMessageProcessor> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    [Function(nameof(PoisonMessageProcessor))]
    public async Task Run(
        [QueueTrigger("%PoisonQueueName%", Connection = "AzureWebJobsStorage")]
        string rawMessage,
        FunctionContext context)
    {
        _logger.LogError(
            "Poison message received. The message exceeded the maximum dequeue count " +
            "and could not be processed successfully. Raw content: {RawMessage}",
            rawMessage);

        var taskMessage = TryDeserialise(rawMessage);

        if (taskMessage is not null)
        {
            await HandlePoisonTaskAsync(taskMessage);
        }
        else
        {
            _logger.LogWarning(
                "Could not deserialise poison message — storing raw content for manual review.");
            await StoreRawPoisonMessageAsync(rawMessage);
        }
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private async Task HandlePoisonTaskAsync(TaskMessage message)
    {
        _logger.LogError(
            "Poison task: TaskId={TaskId}, TaskType={TaskType}, EnqueuedAt={EnqueuedAt}",
            message.TaskId, message.TaskType, message.EnqueuedAt);

        // Example strategy: send an alert and persist for manual review.
        // In production replace these with your alerting / persistence service.
        await Task.WhenAll(
            SendAlertAsync(message),
            PersistForManualReviewAsync(message));
    }

    private Task SendAlertAsync(TaskMessage message)
    {
        // Placeholder: integrate with your notification system (e.g. send an email,
        // post to a Teams/Slack webhook, create a PagerDuty incident).
        _logger.LogWarning(
            "[ALERT] Poison message for task {TaskId} requires manual intervention.",
            message.TaskId);
        return Task.CompletedTask;
    }

    private Task PersistForManualReviewAsync(TaskMessage message)
    {
        // Placeholder: write the message to a Table Storage or Cosmos DB audit table
        // so an operator can inspect and optionally replay it.
        _logger.LogInformation(
            "Persisting poison task {TaskId} for manual review.", message.TaskId);
        return Task.CompletedTask;
    }

    private Task StoreRawPoisonMessageAsync(string rawMessage)
    {
        _logger.LogInformation(
            "Storing raw poison message ({Length} bytes) for manual review.", rawMessage.Length);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to decode the raw queue message and deserialise it as a <see cref="TaskMessage"/>.
    /// Queue messages are Base64-encoded; this method handles both encoded and plain-text variants.
    /// </summary>
    private static TaskMessage? TryDeserialise(string rawMessage)
    {
        try
        {
            // Try to decode Base64 first (default encoding used by the SDK).
            string json = rawMessage;
            try
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(rawMessage));
            }
            catch (FormatException)
            {
                // Not Base64 — assume the content is already plain JSON.
            }

            return JsonSerializer.Deserialize<TaskMessage>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
