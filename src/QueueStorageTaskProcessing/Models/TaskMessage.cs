namespace QueueStorageTaskProcessing.Models;

/// <summary>
/// Represents a task message stored in Azure Queue Storage.
/// Queue Storage messages are Base64-encoded; the trigger automatically decodes them.
/// Maximum raw message size is 64 KB — use a Blob Storage reference for larger payloads.
/// </summary>
public class TaskMessage
{
    /// <summary>Unique identifier for the task.</summary>
    public string TaskId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The type of work to perform (e.g. "SendEmail", "GenerateReport").</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>Serialised payload for the task. Keep under 64 KB.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to a Blob Storage object that holds a large payload.
    /// Use this when the message content would exceed the 64 KB Queue Storage limit.
    /// </summary>
    public string? BlobPayloadReference { get; set; }

    /// <summary>UTC timestamp when the message was originally enqueued.</summary>
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Priority hint for the consumer (lower number = higher priority).</summary>
    public int Priority { get; set; } = 5;
}
