using Azure.Storage.Queues.Models;
using QueueStorageTaskProcessing.Models;

namespace QueueStorageTaskProcessing.Services;

/// <summary>
/// Abstracts Azure Queue Storage operations used by the Azure Functions.
/// </summary>
public interface IQueueService
{
    /// <summary>Enqueues a task message, serialising it as JSON and Base64-encoding it.</summary>
    Task EnqueueTaskAsync(TaskMessage message, string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the visibility timeout of a message that is still being processed.
    /// Call this before the current timeout expires to avoid duplicate delivery.
    /// Returns the new popReceipt — always use the latest value for subsequent update/delete calls.
    /// </summary>
    Task<string> ExtendVisibilityTimeoutAsync(
        string queueName,
        string messageId,
        string popReceipt,
        TimeSpan extension,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a successfully processed message from the queue.</summary>
    Task DeleteMessageAsync(
        string queueName,
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks at the next message in the queue without changing its visibility.
    /// </summary>
    Task<PeekedMessage?> PeekMessageAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>Returns the approximate number of messages currently in the queue.</summary>
    Task<int> GetQueueDepthAsync(string queueName, CancellationToken cancellationToken = default);
}
