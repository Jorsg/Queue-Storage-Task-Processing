public class TaskRequest
{
    public string TaskType { get; set; }= string.Empty;
    public object PayLoad { get; set; } = new();
}