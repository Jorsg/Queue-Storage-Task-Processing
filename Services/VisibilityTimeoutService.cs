using Azure.Storage.Queues;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace TaskQueueAPP;

public class VisibilityTimeoutService
{
    private readonly ILogger<VisibilityTimeoutService> _logger;
    private CancellationTokenSource? _cts;

    public VisibilityTimeoutService(ILogger<VisibilityTimeoutService> logger)
    {
        _logger = logger;
    }

    ///<summary>
    /// </summary>
    public void StartRenewing(
        QueueClient queueClient,
        string messageId,
        string popReceipt,
        TimeSpan visibilityTimeout,
        TimeSpan renewInterval)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var currentPopReceipt = popReceipt;

        _ = Task.Run(async () =>
        {
          while (!token.IsCancellationRequested)
          {
            try
            {
                await Task.Delay(renewInterval, token);
                var result = await queueClient.UpdateMessageAsync(
                    messageId,
                    currentPopReceipt,
                    visibilityTimeout: visibilityTimeout
                );

                currentPopReceipt = result.Value.PopReceipt;

                _logger.LogInformation("Extended visibility for message {MessageId} by {Seconds}s", messageId, visibilityTimeout.TotalSeconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stopped visibility renewal for message {MessageId}", messageId);
                break;
            }
            catch(Exception ex)
                {
                    _logger.LogError(ex, $"Failed to extend viisbility for message {messageId}");
                }
          }

        },token);
    }
    
    public void StopRenewing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }



}