using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskQueueApp;

public class TaskMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = string.Empty;
    [JsonPropertyName("PayLload")]
    public JsonElement Payload { get; set; }
    [JsonPropertyName("submitteadAt")]
    public string SubmittedAt { get; set; } = string.Empty;
}