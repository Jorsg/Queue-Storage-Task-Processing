using Microsoft.Extensions.Logging.Abstractions;
using QueueStorageTaskProcessing.Models;

namespace QueueStorageTaskProcessing.Tests;

/// <summary>
/// Unit tests for <see cref="TaskMessage"/> model validation and serialisation.
/// </summary>
public class TaskMessageTests
{
    [Fact]
    public void TaskMessage_DefaultValues_AreSet()
    {
        var message = new TaskMessage();

        Assert.False(string.IsNullOrEmpty(message.TaskId), "TaskId should be auto-generated");
        Assert.Equal(string.Empty, message.TaskType);
        Assert.Equal(string.Empty, message.Payload);
        Assert.Equal(5, message.Priority);
        Assert.True(message.EnqueuedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void TaskMessage_SerialiseAndDeserialise_RoundTrips()
    {
        var original = new TaskMessage
        {
            TaskId = "task-001",
            TaskType = "SendEmail",
            Payload = "{ \"to\": \"user@example.com\" }",
            Priority = 1,
            EnqueuedAt = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<TaskMessage>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.TaskId, restored.TaskId);
        Assert.Equal(original.TaskType, restored.TaskType);
        Assert.Equal(original.Payload, restored.Payload);
        Assert.Equal(original.Priority, restored.Priority);
        Assert.Equal(original.EnqueuedAt, restored.EnqueuedAt);
    }

    [Fact]
    public void TaskMessage_Base64EncodeDecode_PreservesContent()
    {
        // Queue Storage messages are Base64-encoded on the wire.
        // Verify our encode/decode round-trip works correctly.
        var original = new TaskMessage
        {
            TaskId = "task-002",
            TaskType = "GenerateReport",
            Payload = "quarterly-2024"
        };

        var json = JsonSerializer.Serialize(original);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        // Simulate what a consumer does: Base64-decode then JSON-deserialise.
        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var restored = JsonSerializer.Deserialize<TaskMessage>(decodedJson);

        Assert.NotNull(restored);
        Assert.Equal(original.TaskId, restored.TaskId);
        Assert.Equal(original.TaskType, restored.TaskType);
        Assert.Equal(original.Payload, restored.Payload);
    }

    [Fact]
    public void TaskMessage_PayloadSizeUnder64KB_IsValid()
    {
        // Queue Storage hard limit is 64 KB per message.
        const int maxQueueMessageBytes = 64 * 1024;

        var message = new TaskMessage
        {
            TaskId = "task-003",
            TaskType = "SendEmail",
            // Create a payload that is safely under the limit.
            Payload = new string('x', 1024)
        };

        var json = JsonSerializer.Serialize(message);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Assert.True(
            Encoding.UTF8.GetByteCount(encoded) < maxQueueMessageBytes,
            $"Encoded message must be under {maxQueueMessageBytes} bytes (Queue Storage limit)");
    }

    [Fact]
    public void TaskMessage_WithBlobReference_AllowsLargePayloadPattern()
    {
        // When the payload exceeds 64 KB, store the data in Blob Storage and put the
        // blob reference in the message.
        var message = new TaskMessage
        {
            TaskId = "task-004",
            TaskType = "ProcessFile",
            Payload = string.Empty,
            BlobPayloadReference = "task-payloads/task-004.json"
        };

        Assert.NotNull(message.BlobPayloadReference);
        Assert.Empty(message.Payload);
    }
}
