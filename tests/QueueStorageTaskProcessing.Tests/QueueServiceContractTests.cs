using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QueueStorageTaskProcessing.Models;
using QueueStorageTaskProcessing.Services;

namespace QueueStorageTaskProcessing.Tests;

/// <summary>
/// Unit tests for visibility timeout and queue depth patterns.
/// These tests verify the contract of <see cref="IQueueService"/> without touching real Azure Storage.
/// </summary>
public class QueueServiceContractTests
{
    [Fact]
    public async Task ExtendVisibilityTimeout_InvokesServiceWithCorrectParameters()
    {
        var mock = new Mock<IQueueService>();
        mock.Setup(s => s.ExtendVisibilityTimeoutAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-pop-receipt");

        const string queueName = "task-queue";
        const string messageId = "msg-001";
        const string popReceipt = "receipt-abc";
        var extension = TimeSpan.FromSeconds(30);

        var newReceipt = await mock.Object.ExtendVisibilityTimeoutAsync(queueName, messageId, popReceipt, extension);

        Assert.Equal("new-pop-receipt", newReceipt);
        mock.Verify(s => s.ExtendVisibilityTimeoutAsync(
            queueName,
            messageId,
            popReceipt,
            extension,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DeleteMessage_InvokesServiceWithCorrectParameters()
    {
        var mock = new Mock<IQueueService>();
        mock.Setup(s => s.DeleteMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        const string queueName = "task-queue";
        const string messageId = "msg-002";
        const string popReceipt = "receipt-xyz";

        await mock.Object.DeleteMessageAsync(queueName, messageId, popReceipt);

        mock.Verify(s => s.DeleteMessageAsync(
            queueName,
            messageId,
            popReceipt,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GetQueueDepth_ReturnsApproximateMessageCount()
    {
        var mock = new Mock<IQueueService>();
        mock.Setup(s => s.GetQueueDepthAsync("task-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var depth = await mock.Object.GetQueueDepthAsync("task-queue");

        Assert.Equal(42, depth);
    }

    [Fact]
    public async Task EnqueueTask_SerializesAndEnqueuesMessage()
    {
        var mock = new Mock<IQueueService>();
        TaskMessage? capturedMessage = null;
        string? capturedQueueName = null;

        mock.Setup(s => s.EnqueueTaskAsync(
                It.IsAny<TaskMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaskMessage, string, CancellationToken>((msg, q, _) =>
            {
                capturedMessage = msg;
                capturedQueueName = q;
            })
            .Returns(Task.CompletedTask);

        var message = new TaskMessage
        {
            TaskId = "task-enqueue-test",
            TaskType = "SendEmail",
            Payload = "hello"
        };

        await mock.Object.EnqueueTaskAsync(message, "task-queue");

        Assert.Equal("task-enqueue-test", capturedMessage?.TaskId);
        Assert.Equal("task-queue", capturedQueueName);
    }

    [Fact]
    public void QueueName_MustBeLowercase_ConventionCheck()
    {
        // Queue names must be lowercase, alphanumeric, and may contain hyphens.
        // Uppercase or underscore characters are not allowed by the Azure Storage service.
        var validNames = new[] { "task-queue", "myqueue", "task-queue-poison" };
        var invalidNames = new[] { "TaskQueue", "task_queue", "MYQUEUE" };

        foreach (var name in validNames)
        {
            Assert.True(IsValidQueueName(name), $"'{name}' should be a valid queue name");
        }

        foreach (var name in invalidNames)
        {
            Assert.False(IsValidQueueName(name), $"'{name}' should be an invalid queue name");
        }
    }

    private static bool IsValidQueueName(string name)
    {
        // Azure Queue Storage naming rules: 3–63 characters, lowercase alphanumeric and hyphens,
        // must start and end with a letter or number, no consecutive hyphens.
        if (name.Length < 3 || name.Length > 63) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")) return false;
        if (name.Contains("--")) return false;
        return true;
    }
}
