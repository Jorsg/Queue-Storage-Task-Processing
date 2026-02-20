using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QueueStorageTaskProcessing.Models;

namespace QueueStorageTaskProcessing.Services;

/// <summary>
/// Implements <see cref="IQueueService"/> using Azure Queue Storage.
///
/// Key design notes:
/// • Messages are always Base64-encoded when sent (SDK default) so they survive the
///   64 KB size limit and wire encoding unchanged.
/// • Use <see cref="ExtendVisibilityTimeoutAsync"/> to renew the visibility lease for
///   tasks that take longer than the configured visibilityTimeout in host.json.
/// • Queue names must be lowercase and alphanumeric (with hyphens).
/// </summary>
public class QueueService : IQueueService
{
    private readonly QueueServiceClient _serviceClient;
    private readonly ILogger<QueueService> _logger;

    public QueueService(IConfiguration configuration, ILogger<QueueService> logger)
    {
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is not configured.");
        _serviceClient = new QueueServiceClient(connectionString);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EnqueueTaskAsync(
        TaskMessage message,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOrCreateQueueAsync(queueName, cancellationToken);

        // Serialise to JSON, then Base64-encode — matching the SDK's default message encoding.
        var json = JsonSerializer.Serialize(message);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        await client.SendMessageAsync(encoded, cancellationToken: cancellationToken);
        _logger.LogInformation("Enqueued task {TaskId} (type: {TaskType}) to queue {QueueName}",
            message.TaskId, message.TaskType, queueName);
    }

    /// <inheritdoc/>
    public async Task<string> ExtendVisibilityTimeoutAsync(
        string queueName,
        string messageId,
        string popReceipt,
        TimeSpan extension,
        CancellationToken cancellationToken = default)
    {
        var client = _serviceClient.GetQueueClient(queueName);

        // UpdateMessage resets the invisible window and returns a new popReceipt.
        // Always use the latest popReceipt for subsequent update or delete calls.
        var response = await client.UpdateMessageAsync(
            messageId,
            popReceipt,
            visibilityTimeout: extension,
            cancellationToken: cancellationToken);

        _logger.LogDebug(
            "Extended visibility timeout for message {MessageId} in queue {QueueName} by {Extension}",
            messageId, queueName, extension);

        return response.Value.PopReceipt;
    }

    /// <inheritdoc/>
    public async Task DeleteMessageAsync(
        string queueName,
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default)
    {
        var client = _serviceClient.GetQueueClient(queueName);
        await client.DeleteMessageAsync(messageId, popReceipt, cancellationToken);
        _logger.LogDebug("Deleted message {MessageId} from queue {QueueName}", messageId, queueName);
    }

    /// <inheritdoc/>
    public async Task<PeekedMessage?> PeekMessageAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var client = _serviceClient.GetQueueClient(queueName);
        var response = await client.PeekMessageAsync(cancellationToken);
        return response?.Value;
    }

    /// <inheritdoc/>
    public async Task<int> GetQueueDepthAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        var client = _serviceClient.GetQueueClient(queueName);
        var properties = await client.GetPropertiesAsync(cancellationToken);
        var depth = properties.Value.ApproximateMessagesCount;
        _logger.LogInformation("Queue {QueueName} has approximately {Depth} messages", queueName, depth);
        return depth;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<QueueClient> GetOrCreateQueueAsync(
        string queueName,
        CancellationToken cancellationToken)
    {
        var client = _serviceClient.GetQueueClient(queueName);
        await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return client;
    }
}
