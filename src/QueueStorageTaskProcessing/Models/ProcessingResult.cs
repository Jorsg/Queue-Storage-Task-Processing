namespace QueueStorageTaskProcessing.Models;

/// <summary>
/// Outcome of processing a single <see cref="TaskMessage"/>.
/// </summary>
public class ProcessingResult
{
    public string TaskId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
    public int DequeueCount { get; set; }
}
