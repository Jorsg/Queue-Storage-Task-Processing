using System;
using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TaskQueueApp;

public class ProcessTask
{
    private readonly ILogger<ProcessTask> _logger;

    public ProcessTask(ILogger<ProcessTask> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ProcessTask))]
    public async Task Run([QueueTrigger("task-queue", Connection = "AzureStorageConnection")] string messageText, long DequeueCount)
    {
        _logger.LogInformation($"Processing message | Attempt: {DequeueCount} /5");

        try
        {
            //Deserilize the message
            var task = JsonSerializer.Deserialize<TaskMessage>(messageText);
            if(task == null)
            {
                _logger.LogWarning($"Failed to deserialize message {messageText}");
                return;
            }
            _logger.LogInformation($"Processing Task Id {task.Id} | Type: {task.TaskType} | Attempt: {DequeueCount} /5");

            if (DequeueCount >= 5)
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
        _logger.LogInformation($"Generating report for task {task.Id}");
        await Task.Delay(1000);
        _logger.LogInformation($"Report generated for tas {task.Id}");
    }
}