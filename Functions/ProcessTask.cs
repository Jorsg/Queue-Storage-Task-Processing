using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TaskQueueAPP.Services;
using TaskQueueAPP.Models;

namespace TaskQueueAPP;

public class ProcessTask
{
    private readonly ILogger<ProcessTask> _logger;
    private readonly VisibilityTimeoutService _visibilityService;
    private readonly TaskResultService _resultService;
    private readonly MetricService _metrics;

    public ProcessTask(ILogger<ProcessTask> logger, VisibilityTimeoutService visibilityTimeoutService, 
                       TaskResultService resultService,
                       MetricService metrics)
    {
        _logger = logger;
        _visibilityService = visibilityTimeoutService;
        _resultService = resultService;
        _metrics = metrics;
    }

    [Function(nameof(ProcessTask))]
    public async Task Run([QueueTrigger("task-queue", Connection = "AzureStorageConnection")] string messageText,
                                        string Id,
                                        string popReceipt,
                                        long DequeueCount)
    {
        // _logger.LogInformation("Processing message {MessageId} | Attempt: {DequeueCount}/5", Id, DequeueCount);
        var stopwatch = Stopwatch.StartNew();
        QueueClient? queueClient = null;
        var isLongRunning = false;
        TaskMessage? task = null;

        try
        {
            //Deserilize the message
            task = JsonSerializer.Deserialize<TaskMessage>(messageText);
            if (task == null)
            {
                _logger.LogWarning($"Failed to deserialize message {messageText}");
                return;
            }

            _logger.LogInformation($"Processing Task {task.Id} | Type: {task.TaskType} | Attempt: {DequeueCount}/5");

            isLongRunning = IsLongRunningTask(task.TaskType);
            if (isLongRunning)
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection");
                queueClient = new QueueClient(connectionString, "task-queue");

                _visibilityService.StartRenewing(
                    queueClient: queueClient,
                    messageId: Id,
                    popReceipt: popReceipt,
                    visibilityTimeout: TimeSpan.FromMinutes(2),
                    renewInterval: TimeSpan.FromSeconds(60)
                );
                //_logger.LogInformation("Started visibility renewal for long-running task {TaskId}", task.Id);
            }


            // _logger.LogInformation($"Processing Task Id {task.Id} | Type: {task.TaskType} | Attempt: {DequeueCount} /5");

            if (DequeueCount >= 3)
            {
                _logger.LogWarning($"Task {task.Id} is on {DequeueCount} /5 - may become poison");
            }


            //Process the task

            var result = task.TaskType?.ToLower() switch
            {
                "sendemail" => await HandleSendEmail(task),
                "generatereport" => await HandleGenerateReport(task),
                _ => "Unknow task type"
            };

            stopwatch.Stop();

            await _resultService.SaveSuccessAsync(
                taskId: task.Id,
                taskType: task.TaskType ?? "Unknown",
                submittedAt: DateTime.Parse(task.SubmittedAt),
                payload: task.Payload.ToString(),
                result: result,
                attempt: (int)DequeueCount,
                durationMs: stopwatch.Elapsed.TotalMilliseconds
            );

            _metrics.TrackTaskCompleted(task.Id, task.TaskType ?? "Unknown", stopwatch.Elapsed.TotalMilliseconds, (int)DequeueCount);
            _logger.LogInformation($"Task {task.Id} completed in {stopwatch.Elapsed.TotalMilliseconds}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Save failure to Table Storage
            if (task != null)
            {
                await _resultService.SaveFailureAsync(
                    taskId: task.Id,
                    taskType: task.TaskType,
                    submittedAt: DateTime.Parse(task.SubmittedAt),
                    payload: task.Payload.ToString(),
                    errorMessage: ex.Message,
                    attempts: (int)DequeueCount);

                    _metrics.TrackTaskFailed(task.Id, task.TaskType, ex.Message, (int)DequeueCount);
            }
            _logger.LogError(ex, $"Error failed on attempt {DequeueCount}/5. Message {messageText}");
            throw;
        }
        finally
        {
            // Always stop renewing when done (success or failure)
            if (isLongRunning)
            {
                _visibilityService.StopRenewing();
                _logger.LogInformation("Stopped visibility renewal for message {Id}", Id);
            }
        }
    }

    private bool IsLongRunningTask(string TaskType)
    {
        var longRunningTask = new[] { "generatereport", "processfile", "datamigration" };
        return longRunningTask.Contains(TaskType?.ToLower());
    }


    private async Task<string> HandleSendEmail(TaskMessage task)
    {
        _logger.LogInformation($"Sending email for task {task.Id}");
        await Task.Delay(500);
        return $"Email sent successfuly to {task.Payload}";
        // throw new Exception("Simulated failure for testing");
    }

    private async Task<string> HandleGenerateReport(TaskMessage task)
    {
        //TODO: Implement real report logic later
        _logger.LogInformation("Generating report for task {TaskId}...", task.Id);
        await Task.Delay(TimeSpan.FromMinutes(3)); // simulate long-running work (triggers renewals at 60s, 120s)
        return $"Report generated: report_{task.Id}.pdf";
    }
}