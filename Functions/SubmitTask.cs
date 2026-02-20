using System.Text.Json;
using Azure.Storage.Queues;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace TaskQueueAPP
{
    public class SubmitTask
    {
        private readonly ILogger<SubmitTask> _logger;

        public SubmitTask(ILogger<SubmitTask> logger)
        {
            _logger = logger;
        }

        [Function("SubmitTask")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            //Deserialize requet
            var body = await req.ReadFromJsonAsync<TaskRequest>();

            if (body == null || string.IsNullOrEmpty(body.TaskType) || body.PayLoad == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new {error = "Missing 'TaskType' or 'payLoad'"});
                return badResponse;
            }
            //build message
            var TaskId = Guid.NewGuid().ToString();
            var message = new
            {
              id = TaskId,
              taskType = body.TaskType,
              payload = body.PayLoad,
              submittedAt = DateTime.UtcNow.ToString("o")  
            };

            //Enqueue
            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection");
            var queueClient = new QueueClient(connectionString, "task-queue");
            await queueClient.CreateIfNotExistsAsync();

            var jsonMessage = JsonSerializer.Serialize(message);
            var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonMessage));
            await queueClient.SendMessageAsync(base64Message);

            _logger.LogInformation($"Task enqueue: {TaskId}");

            //Response

            var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
            await response.WriteAsJsonAsync( new TaskResponse
            {
               Success = true,
               TaskId = TaskId,
               Menssage = "Task enqueue" 
            });

            return response;
        }
    }
}