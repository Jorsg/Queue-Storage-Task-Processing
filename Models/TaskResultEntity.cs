using Azure;
using Azure.Data.Tables;

namespace TaskQueueAPP.Models;

public class TaskResultEntity: ITableEntity
{
    //PartitionKey = date (yyyy-MM-dd) for efficient querying by day
     public string PartitionKey { get; set; } = string.Empty;


     //RowKey = taskId for uniqueness
     public string RowKey { get; set; } = string.Empty;

     public string TaskType { get; set; } = string.Empty;
     public string Status { get; set; } = string.Empty; // Completed, Failed, Poison
     public string Payload { get; set; } = string.Empty;
     public string Result { get; set; } = string.Empty;
     public string ErrorMessage { get; set; } = string.Empty;
     public int Attempt { get; set; }
     public DateTime SubmittedAt { get; set; }
     public DateTime CompletedAt { get; set; }
     public double DurationMs { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public static TaskResultEntity Create(string taskId, string taskType, DateTime submittedAt)
    {
        return new TaskResultEntity
        {
            PartitionKey = submittedAt.ToString("yyyy-MM-dd"),
            RowKey = taskId,
            TaskType = taskType,
            SubmittedAt = submittedAt
        };
    }
     
}