# Queue-Storage-Task-Processing

A task processing system built with **Azure Functions (.NET 8 Isolated Worker)** and **Azure Queue Storage** that demonstrates queue-based task processing, poison message handling, and visibility timeout patterns.

---

## Project Structure

```
├── src/
│   └── QueueStorageTaskProcessing/
│       ├── Functions/
│       │   ├── TaskQueueProcessor.cs        # Queue trigger: Base64 decode, dequeue tracking
│       │   ├── PoisonMessageProcessor.cs    # Poison queue handler
│       │   └── LongRunningTaskProcessor.cs  # Visibility timeout extension pattern
│       ├── Models/
│       │   ├── TaskMessage.cs               # Queue message model (≤ 64 KB)
│       │   └── ProcessingResult.cs          # Processing outcome
│       ├── Services/
│       │   ├── IQueueService.cs             # Queue operations interface
│       │   └── QueueService.cs              # Azure Queue Storage implementation
│       ├── host.json                        # Poison threshold, batch size, polling
│       ├── local.settings.json              # Local dev connection strings
│       └── Program.cs                       # DI / host builder
└── tests/
    └── QueueStorageTaskProcessing.Tests/
        ├── TaskMessageTests.cs              # Model + Base64 encode/decode
        ├── PoisonMessageProcessorTests.cs   # Deserialisation edge cases
        └── QueueServiceContractTests.cs     # Service contract + queue name rules
```

---

## Key Concepts

### 1. Queue Storage Operations

- **Enqueue** — serialise a `TaskMessage` as JSON, Base64-encode it, and send it with `QueueClient.SendMessageAsync`.
- **Delete** — call `DeleteMessageAsync` with the `messageId` and `popReceipt` after successful processing.
- **Peek** — inspect the next message without changing its visibility via `PeekMessageAsync`.
- **Queue depth** — monitor with `GetPropertiesAsync().ApproximateMessagesCount` for custom telemetry/autoscaling.

### 2. Poison Message Handling

When a message's delivery count exceeds `maxDequeueCount` (configured in `host.json`, default **5**), the Azure Functions runtime automatically moves it to `<queueName>-poison`.

```json
// host.json
"extensions": {
  "queues": {
    "maxDequeueCount": 5
  }
}
```

`PoisonMessageProcessor` triggers on `task-queue-poison` and:
- Logs at `Error` level
- Attempts to deserialise the raw (possibly Base64-encoded) message body
- Sends an alert and persists the message for manual review

### 3. Visibility Timeout Patterns

Default visibility timeout is **30 seconds**. For tasks that take longer:

1. Receive the message via the SDK (not the binding) to capture `messageId` + `popReceipt`.
2. Start a background renewal loop that calls `UpdateMessageAsync` every 20 seconds.
3. On success, delete the message; on failure, let it become visible again for re-delivery.

See `LongRunningTaskProcessor.cs` for a complete implementation.

### 4. Message Encoding

Queue Storage messages are **Base64-encoded** by default (SDK behaviour):

```csharp
// Enqueue
var json    = JsonSerializer.Serialize(message);
var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
await client.SendMessageAsync(encoded);

// Consume (the Queue trigger decodes automatically)
// Manual decode when using ReceiveMessageAsync:
var json = Encoding.UTF8.GetString(Convert.FromBase64String(body));
```

### 5. 64 KB Message Size Limit

Queue Storage messages are limited to **64 KB**. For larger payloads:
- Store the data in **Blob Storage**.
- Set `TaskMessage.BlobPayloadReference` to the blob path.
- The consumer reads the blob using the reference.

### 6. At-Least-Once Delivery

Queue Storage guarantees at-least-once delivery (no built-in duplicate detection):
- Use `dequeueCount` to detect and handle duplicate processing (idempotent consumers).
- Apply different strategies at different retry levels (e.g. skip heavy work on the first attempt, escalate alerting after 3 retries).

### 7. Queue vs Service Bus — When to Use Which

| Criteria | Queue Storage | Service Bus |
|---|---|---|
| Cost | Lower (pay per operation) | Higher (per namespace/hour) |
| Message size | 64 KB | 256 KB (Standard) / 100 MB (Premium) |
| Ordering | No guarantee | FIFO with sessions |
| Duplicate detection | No | Yes |
| Dead-letter queue | Manual (`-poison` naming) | Built-in |
| Max retention | 7 days | 14 days |
| Throughput | Very high | High |
| **Use when** | Simple task queues, high-volume, cost-sensitive | Ordered processing, transactions, pub/sub, strict dedup |

---

## Running Locally

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local Azure Storage emulator)

### Start Azurite
```bash
azurite --silent --location /tmp/azurite
```

### Run the Functions Host
```bash
cd src/QueueStorageTaskProcessing
func start
```

### Run Tests
```bash
dotnet test
```

---

## Configuration Reference (`host.json`)

| Setting | Default | Description |
|---|---|---|
| `maxDequeueCount` | 5 | Retries before poison queue |
| `visibilityTimeout` | 00:00:30 | Initial message invisibility window |
| `batchSize` | 16 | Messages fetched per worker poll |
| `maxPollingInterval` | 00:02:00 | Idle queue poll frequency |
| `newBatchThreshold` | 8 | Worker count before fetching a new batch |
