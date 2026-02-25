using System.Net;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TaskQueueApp.Services;

namespace TaskQueueAPP
{
    public class GetTaskResult
    {
        private readonly ILogger<GetTaskResult> _logger;
        private readonly TaskResultService _resultServices;

        public GetTaskResult(ILogger<GetTaskResult> logger, TaskResultService resultService)
        {
            _logger = logger;
            _resultServices = resultService;
        }

        //Get /api/tasks/{date} - get all results for a date
        [Function("GetTaskByDate")]
        public async Task<HttpResponseData> GetByDate(
            [HttpTrigger(AuthorizationLevel.Anonymous,"get", Route = "tasks/{date}")] HttpRequestData req,
             string date)
        {
            _logger.LogInformation($"Fetching tasks for date {date}");

            var result = await _resultServices.GetByDateAsync(date);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;            
        }

        [Function("GetTaskbyId")]
        public async Task<HttpResponseData> GetById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/{date}/{taskId}")] HttpRequestData req,
            string date,
            string taskId)
        {
            _logger.LogInformation($"Fetching task {taskId} for date {date}");

            var result = await _resultServices.GetByIdAsync(taskId, date);
            if(result == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new {error = "Task not found"});
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
            
        }




    }
    
}