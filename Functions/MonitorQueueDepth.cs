using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TaskQueueAPP.Services;


namespace TaskQueueAPP
{
    public class MonitorQueueDepth
    {
        private readonly ILogger<MonitorQueueDepth> _logger;
        private readonly MetricService _metrics;

        public MonitorQueueDepth(ILogger<MonitorQueueDepth> logger, MetricService metrics)
        {
            _logger = logger;
            _metrics = metrics;
        }

        [Function("MonitorQueueDepth")]
        public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
        {
            _logger.LogInformation($"Queue depth check at {DateTime.UtcNow}");
            await _metrics.TrackQueueDepthAysnc();
        }
    }
}