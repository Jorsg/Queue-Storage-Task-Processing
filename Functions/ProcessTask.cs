using System;
using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TaskQueueAPP;

namespace TaskQueueApp;

public class ProcessTask
{
    private readonly ILogger<ProcessTask> _logger;
    private readonly VisibilityTimeoutService _visibilityService;

    public ProcessTask(ILogger<ProcessTask> logger, VisibilityTimeoutService visibilityTimeoutService)
    {
        _logger = logger;
        _visibilityService = visibilityTimeoutService;
    }

    [Function(nameof(ProcessTask))]
    public async Task Run([QueueTrigger("task-queue", Connection = "AzureStorageConnection")] string messageText, 
                                        string Id, string popReceipt, long DequeueCount)
    {
        _logger.LogInformation("Processing message {MessageId} | Attempt: {DequeueCount}/5", Id, DequeueCount);
        QueueClient? queueClient = null;
        var isLongRunning = false;

        try
        {
            //Deserilize the message
            var task = JsonSerializer.Deserialize<TaskMessage>(messageText);
            if(task == null)
            {
                _logger.LogWarning($"Failed to deserialize message {messageText}");
                return;
            }

            isLongRunning = IsLongRunningTask(task.TaskType);
            if(isLongRunning)
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
                _logger.LogInformation("Started visibility renewal for long-running task {TaskId}", task.Id);
            }

            
            _logger.LogInformation($"Processing Task Id {task.Id} | Type: {task.TaskType} | Attempt: {DequeueCount} /5");

            if (DequeueCount >= 3)
            {
                _logger.LogWarning($"Task {task.Id} is on {DequeueCount} /5 - may become poison");
            }

            switch(task.TaskType?.ToLower())
            {
                case "sendemail":
                     await HandleSendEmail(task);
                     break;
                case "generatereport":
                      await HandleGenerateReport(task);
                      break;
                default:
                _logger.LogWarning($"Unknow task type: {task.TaskType}");
                break;
            }

            _logger.LogInformation($"Task {task.Id} completed successfully");
        }
        catch(Exception ex)
        {
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
        var longRunningTask = new [] {"generatereport", "processfile", "datamigration"};
        return longRunningTask.Contains(TaskType?.ToLower());
    }


    private async Task HandleSendEmail(TaskMessage task)
    {
        _logger.LogInformation($"Sending email for task {task.Id}");
        await Task.Delay(500);
        _logger.LogInformation($"Email send for task {task.Id}"); 
       // throw new Exception("Simulated failure for testing");
    }

    private async Task HandleGenerateReport(TaskMessage task)
    {
        //TODO: Implement real report logic later
        _logger.LogInformation("Generating report for task {TaskId}...", task.Id);
        await Task.Delay(TimeSpan.FromMinutes(3)); // simulate long-running work (triggers renewals at 60s, 120s)
        _logger.LogInformation("Report generated for task {TaskId}", task.Id);
    }
}