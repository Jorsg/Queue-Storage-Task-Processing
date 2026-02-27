using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using TaskQueueAPP.Models;

namespace TaskQueueAPP.Services
{
    public class TaskResultService
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<TaskResultService> _logger;
        private const string TableName = "TaskResult";


        public TaskResultService(ILogger<TaskResultService> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection");
            _tableClient = new TableClient(connectionString, TableName);
            _tableClient.CreateIfNotExists();
        }

        public async Task SaveSuccessAsync(
            string taskId,
            string taskType,
            DateTime submittedAt,
            string payload,
            string result,
            int attempt,
            double durationMs)
        {
            var entity = TaskResultEntity.Create(taskId, taskType, submittedAt);
            entity.Status = "Completed";
            entity.Payload = payload;
            entity.Result = result;
            entity.Attempt = attempt;
            entity.CompletedAt = DateTime.UtcNow;
            entity.DurationMs = durationMs;

            await _tableClient.UpsertEntityAsync(entity);
            _logger.LogInformation($"Saved success result for task {taskId}");
        }

        public async Task SaveFailureAsync(
            string taskId,
            string taskType,
            DateTime submittedAt,
            string payload,
            string errorMessage,
            int attempts)
        {
            var entity = TaskResultEntity.Create(taskId, taskType, submittedAt);
            entity.Status = "Failed";
            entity.Payload = payload;
            entity.ErrorMessage = errorMessage;
            entity.Attempt = attempts;
            entity.CompletedAt = DateTime.UtcNow;

            await _tableClient.UpsertEntityAsync(entity);
            _logger.LogInformation($"Saved failure result for task {taskId}");
        }

        public async Task SavePoisonAsync(
            string taskId,
            string taskType,
            DateTime submittedAt,
            string payload,
            string errorMessage)
        {
            var entity = TaskResultEntity.Create(taskId, taskType, submittedAt);
            entity.Status = "Poison";
            entity.Payload = payload;
            entity.ErrorMessage = errorMessage;
            entity.Attempt = 5;
            entity.CompletedAt = DateTime.UtcNow;

            await _tableClient.UpsertEntityAsync(entity);
            _logger.LogInformation($"Saved poison result for task {taskId}");
        }

        ///<summary>
        /// 
        /// </summary>
        public async Task<List<TaskResultEntity>> GetByDateAsync(string date)
        {
            var result = new List<TaskResultEntity>();

            await foreach (var entity in _tableClient.QueryAsync<TaskResultEntity>(filter: $"PartitionKey eq '{date}'"))
            {
                result.Add(entity);
            }

            return result;
        }

        ///<summary>
        /// 
        /// </summary>
        public async Task<TaskResultEntity?> GetByIdAsync(string taskId, string date)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<TaskResultEntity>(date, taskId);
                return response.Value;
            }
            catch
            {
                return null;
            }

        }


    }
}