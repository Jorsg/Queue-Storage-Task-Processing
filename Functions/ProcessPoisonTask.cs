using System;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TaskQueueApp;

public class ProcessPoisonTask
{
    private readonly ILogger<ProcessPoisonTask> _logger;

    public ProcessPoisonTask(ILogger<ProcessPoisonTask> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ProcessPoisonTask))]
    public async Task Run([QueueTrigger("task-queue-poison", Connection = "AzureStorageConnection")] string messageText)
    {
        _logger.LogCritical($"POISON MESSAGE received at {DateTime.UtcNow}");

        try
        {
            var task = JsonSerializer.Deserialize<TaskMessage>(messageText);
            if (task != null)
            {
                _logger.LogCritical($"Failed Task Details - Id: {task.Id} | Type: {task.TaskType} | Submitted: {task.SubmittedAt} | Payload: {task.Payload}");

                await SaveFailedTask(task, messageText);

                await NotifyTeam(task);

                
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handlog poison message: {messageText}");
            throw;
        }
    }


    private async Task SaveFailedTask(TaskMessage task, string message)
    {
        _logger.LogWarning($"Saving failed task {task.Id} for investigation...");
        await Task.CompletedTask;
    }

    private async Task NotifyTeam(TaskMessage task)
    {
        _logger.LogWarning($"ALERT: Task {task.Id} ({task.TaskType}) failed permanently after 5 attempts");
        await Task.CompletedTask;
    }
}