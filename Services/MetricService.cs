using System.Diagnostics;
using Azure.Storage.Queues;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Metrics;
using Microsoft.Extensions.Logging;

namespace TaskQueueAPP.Services
{
    public class MetricService
    {
        private readonly TelemetryClient _telemetry;
        private readonly ILogger<MetricService> _logger;

        public MetricService(TelemetryClient telemetry, ILogger<MetricService> logger)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        ///<summary>
        /// Track a successfully processed task
        /// </summary>
        public void TrackTaskCompleted(string taskId, string taskType, double durationMs, int attempts)
        {
            var taskCompleteTelemetry = new EventTelemetry("TaskComplete");
            taskCompleteTelemetry.Properties["Taskid"] = taskId;
            taskCompleteTelemetry.Properties["TaskType"] = taskType;
            taskCompleteTelemetry.Properties["Attempts"] = attempts.ToString();
            taskCompleteTelemetry.Properties["DurationMs"] = durationMs.ToString();
            taskCompleteTelemetry.Properties["AttemptCount"] = attempts.ToString();
            _telemetry.TrackEvent(taskCompleteTelemetry);

            // Custom metrics
            _telemetry.GetMetric("TaskProcessingTime", "TaskType").TrackValue(durationMs, taskType);
            _telemetry.GetMetric("TaskSuccessCount", "TaskType").TrackValue(1, taskType);

            _logger.LogInformation($"Tracked completion — Task {taskId} | {taskType} | {durationMs}ms | Attempts: {attempts}");

        }

        /// <summary>
        /// Track a failed task attempt
        /// </summary>
         public void TrackTaskFailed(string taskId, string taskType, string error, int attempt)
        {
            _telemetry.TrackEvent("TaskFailed", new Dictionary<string, string>
            {
                { "TaskId", taskId },
                { "TaskType", taskType },
                { "Error", error },
                { "Attempt", attempt.ToString() }
            });

            _telemetry.GetMetric("TaskFailureCount", "TaskType").TrackValue(1, taskType);

            // Track as exception for App Insights Failures blade
            _telemetry.TrackException(new ExceptionTelemetry
            {
                Message = $"Task {taskId} failed on attempt {attempt}: {error}",
                SeverityLevel = attempt >= 4 ? SeverityLevel.Critical : SeverityLevel.Warning,
                Properties =
                {
                    { "TaskId", taskId },
                    { "TaskType", taskType },
                    { "Attempt", attempt.ToString() }
                }
            });
        }

        /// <summary>
        /// Track a poison message
        /// </summary>
        public void TrackTaskPoisoned(string taskId, string taskType)
        {
            _telemetry.TrackEvent("TaskPoisoned", new Dictionary<string, string>
            {
                { "TaskId", taskId },
                { "TaskType", taskType }
            });

            _telemetry.GetMetric("TaskPoisonCount", "TaskType").TrackValue(1, taskType);

            _logger.LogCritical("Tracked POISON — Task {Id} | {Type}", taskId, taskType);
        }

        /// <summary>
        /// Track task submitted via API
        /// </summary>
        public void TrackTaskSubmitted(string taskId, string taskType)
        {
            _telemetry.TrackEvent("TaskSubmitted", new Dictionary<string, string>
            {
                { "TaskId", taskId },
                { "TaskType", taskType }
            });

            _telemetry.GetMetric("TaskSubmittedCount", "TaskType").TrackValue(1, taskType);
        }

        /// <summary>
        /// Track current queue depth
        /// </summary>
        public async Task TrackQueueDepthAysnc()
        {
            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection");
                var taskQueue = new QueueClient(connectionString, "task-queue");
                var poisonQueue = new QueueClient(connectionString, "task-queue-poison");

                var taskProps = await taskQueue.GetPropertiesAsync();
                var poisonProps = await poisonQueue.GetPropertiesAsync();

                var taskDepth = taskProps.Value.ApproximateMessagesCountLong;
                var poisonDepth = poisonProps.Value.ApproximateMessagesCountLong;

                _telemetry.GetMetric("QueueDepth", "QueueName").TrackValue(taskDepth, "task-queue");
                _telemetry.GetMetric("QueueDepth", "QueueName").TrackValue(poisonDepth, "task-queue-poison");

                _logger.LogInformation($"Queue depth - task-queue: {taskDepth}, task-queue-poison: {poisonDepth}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track queue depth");

            }
        }

         /// <summary>
        /// Flush all pending telemetry
        /// </summary>
        public void Flush()
        {
            _telemetry.Flush();
        }
    }
}