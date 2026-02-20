using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QueueStorageTaskProcessing.Models;
using QueueStorageTaskProcessing.Services;

namespace QueueStorageTaskProcessing.Functions;

/// <summary>
/// Demonstrates the visibility timeout extension pattern for long-running tasks.
///
/// Problem: Azure Queue Storage default visibility timeout is 30 seconds.
/// If your task takes longer, the message becomes visible again and is re-delivered —
/// causing duplicate processing (at-least-once delivery semantics).
///
/// Solution: Renew the visibility lease periodically with
/// <see cref="IQueueService.ExtendVisibilityTimeoutAsync"/> before it expires.
///
/// This function uses a <see cref="Timer"/> to renew the lease every
/// <c>VisibilityRenewalInterval</c> while processing is in-flight.
///
/// Note: This pattern requires receiving the message via the SDK (not the binding)
/// so that the messageId and popReceipt are available for the update call.
/// </summary>
public class LongRunningTaskProcessor
{
    /// <summary>How often to renew the visibility lease. Must be less than visibilityTimeout.</summary>
    private static readonly TimeSpan VisibilityRenewalInterval = TimeSpan.FromSeconds(20);

    /// <summary>Each renewal extends the lease by this amount.</summary>
    private static readonly TimeSpan VisibilityExtensionAmount = TimeSpan.FromSeconds(30);

    private readonly IQueueService _queueService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LongRunningTaskProcessor> _logger;

    public LongRunningTaskProcessor(
        IQueueService queueService,
        IConfiguration configuration,
        ILogger<LongRunningTaskProcessor> logger)
    {
        _queueService = queueService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Timer-triggered function that polls the queue for long-running tasks.
    /// Using a timer trigger (rather than a queue trigger) gives full control over
    /// message receipt — including access to messageId and popReceipt needed for
    /// visibility timeout renewal.
    /// </summary>
    [Function(nameof(LongRunningTaskProcessor))]
    public async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo)
    {
        var queueName = _configuration["TaskQueueName"] ?? "task-queue";

        // Retrieve the next available message from the queue with an initial visibility timeout.
        var queueClient = new Azure.Storage.Queues.QueueClient(
            _configuration["AzureWebJobsStorage"],
            queueName);

        var response = await queueClient.ReceiveMessageAsync(
            visibilityTimeout: VisibilityExtensionAmount);

        if (response?.Value is null)
        {
            _logger.LogDebug("No messages available in queue {QueueName}", queueName);
            return;
        }

        var queueMessage = response.Value;

        // Decode and deserialise the message.
        // Queue Storage SDK encodes messages as Base64 by default.
        var taskMessage = DecodeMessage(queueMessage);
        if (taskMessage is null)
        {
            _logger.LogWarning("Could not decode message {MessageId} — skipping", queueMessage.MessageId);
            return;
        }

        _logger.LogInformation(
            "Starting long-running task {TaskId} (type: {TaskType})",
            taskMessage.TaskId, taskMessage.TaskType);

        using var cts = new CancellationTokenSource();
        var popReceipt = queueMessage.PopReceipt;

        // Start a background renewal loop that extends the visibility timeout periodically.
        var renewalTask = RenewVisibilityAsync(
            queueName,
            queueMessage.MessageId,
            () => popReceipt,
            newReceipt => popReceipt = newReceipt,
            cts.Token);

        try
        {
            await ExecuteLongRunningTaskAsync(taskMessage);

            // Success — delete the message so it is not re-delivered.
            await _queueService.DeleteMessageAsync(queueName, queueMessage.MessageId, popReceipt);
            _logger.LogInformation("Task {TaskId} completed and deleted from queue", taskMessage.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed. Message will be re-enqueued after visibility timeout",
                taskMessage.TaskId);
            // Do NOT delete the message — the runtime will re-deliver it.
        }
        finally
        {
            cts.Cancel();
            await renewalTask.ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Periodically extends the message's visibility timeout until <paramref name="cancellationToken"/>
    /// is cancelled. The popReceipt returned by each update replaces the previous one — Azure
    /// Queue Storage issues a new receipt on every <c>UpdateMessage</c> call.
    /// </summary>
    private async Task RenewVisibilityAsync(
        string queueName,
        string messageId,
        Func<string> getPopReceipt,
        Action<string> updatePopReceipt,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(VisibilityRenewalInterval, cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;

                _logger.LogDebug(
                    "Renewing visibility timeout for message {MessageId} in queue {QueueName}",
                    messageId, queueName);

                var newPopReceipt = await _queueService.ExtendVisibilityTimeoutAsync(
                    queueName,
                    messageId,
                    getPopReceipt(),
                    VisibilityExtensionAmount,
                    cancellationToken);

                // Store the new popReceipt so the next renewal (and eventual delete) use it.
                updatePopReceipt(newPopReceipt);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the processing task completes — swallow gracefully.
        }
    }

    private static async Task ExecuteLongRunningTaskAsync(TaskMessage message)
    {
        // Simulates a task that takes longer than the default 30-second visibility timeout.
        await Task.Delay(TimeSpan.FromSeconds(45));
    }

    private static TaskMessage? DecodeMessage(QueueMessage queueMessage)
    {
        try
        {
            var body = queueMessage.Body.ToString();
            // The SDK encodes messages as Base64 by default; decode before deserialising.
            string json = body;
            try
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(body));
            }
            catch (FormatException)
            {
                // Not Base64-encoded — treat as plain JSON.
            }

            return JsonSerializer.Deserialize<TaskMessage>(json);
        }
        catch
        {
            return null;
        }
    }
}
