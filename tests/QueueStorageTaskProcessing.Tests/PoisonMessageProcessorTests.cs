using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QueueStorageTaskProcessing.Functions;
using QueueStorageTaskProcessing.Models;
using QueueStorageTaskProcessing.Services;

namespace QueueStorageTaskProcessing.Tests;

/// <summary>
/// Unit tests for <see cref="PoisonMessageProcessor"/>.
/// </summary>
public class PoisonMessageProcessorTests
{
    private readonly Mock<IQueueService> _queueServiceMock;
    private readonly ILogger<PoisonMessageProcessor> _logger;

    public PoisonMessageProcessorTests()
    {
        _queueServiceMock = new Mock<IQueueService>();
        _logger = NullLogger<PoisonMessageProcessor>.Instance;
    }

    [Fact]
    public void TryDeserialise_PlainJsonMessage_ReturnsTaskMessage()
    {
        var message = new TaskMessage
        {
            TaskId = "poison-001",
            TaskType = "SendEmail",
            Payload = "test"
        };
        var json = JsonSerializer.Serialize(message);

        var result = InvokeDeserialise(json);

        Assert.NotNull(result);
        Assert.Equal("poison-001", result.TaskId);
        Assert.Equal("SendEmail", result.TaskType);
    }

    [Fact]
    public void TryDeserialise_Base64EncodedJsonMessage_ReturnsTaskMessage()
    {
        var message = new TaskMessage
        {
            TaskId = "poison-002",
            TaskType = "GenerateReport"
        };
        var json = JsonSerializer.Serialize(message);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var result = InvokeDeserialise(base64);

        Assert.NotNull(result);
        Assert.Equal("poison-002", result.TaskId);
    }

    [Fact]
    public void TryDeserialise_InvalidJson_ReturnsNull()
    {
        var result = InvokeDeserialise("not-valid-json-or-base64!!!");

        Assert.Null(result);
    }

    [Fact]
    public void TryDeserialise_EmptyString_ReturnsNull()
    {
        var result = InvokeDeserialise(string.Empty);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Helpers — access the private TryDeserialise via a reflection shim.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Invokes the private static <c>TryDeserialise</c> method via reflection.
    /// This keeps the method private while still allowing targeted unit testing of the
    /// Base64 decode + JSON deserialise logic.
    /// </summary>
    private static TaskMessage? InvokeDeserialise(string rawMessage)
    {
        var method = typeof(PoisonMessageProcessor)
            .GetMethod("TryDeserialise",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        return (TaskMessage?)method.Invoke(null, [rawMessage]);
    }
}
