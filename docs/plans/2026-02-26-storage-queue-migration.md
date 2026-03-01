# Storage Queue Migration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace Azure Service Bus with Azure Storage Queues for job processing, enabling full local dev with Azurite.

**Architecture:** Swap `Azure.Messaging.ServiceBus` for `Azure.Storage.Queues`. The API enqueues JSON messages via `QueueClient`. Workers poll queues with `ReceiveMessagesAsync` in a loop. Same Azurite storage account serves both blobs and queues.

**Tech Stack:** Azure.Storage.Queues, ASP.NET 8, Azurite

---

### Task 1: Swap NuGet package

**Files:**
- Modify: `src/MyMemo.Shared/MyMemo.Shared.csproj`

**Step 1: Replace Service Bus package with Storage Queues**

In `MyMemo.Shared.csproj`, replace:
```xml
<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.1" />
```
with:
```xml
<PackageReference Include="Azure.Storage.Queues" Version="12.22.0" />
```

**Step 2: Verify it builds**

Run: `cd /Users/kim/ReposV2/MyMemo/backend && dotnet restore src/MyMemo.Shared`
Expected: Restore succeeds (build will fail — that's expected, we haven't updated the code yet)

---

### Task 2: Replace config class and QueueService

**Files:**
- Modify: `src/MyMemo.Shared/Services/ServiceConfiguration.cs`
- Modify: `src/MyMemo.Shared/Services/QueueService.cs`
- Delete: `src/MyMemo.Shared/Services/NoOpQueueService.cs`

**Step 1: Replace AzureServiceBusOptions with StorageQueueOptions**

In `ServiceConfiguration.cs`, replace:
```csharp
public sealed class AzureServiceBusOptions
{
    public required string ConnectionString { get; init; }
    public string TranscriptionQueueName { get; init; } = "transcription-jobs";
    public string MemoGenerationQueueName { get; init; } = "memo-generation";
}
```
with:
```csharp
public sealed class StorageQueueOptions
{
    public required string ConnectionString { get; init; }
    public string TranscriptionQueueName { get; init; } = "transcription-jobs";
    public string MemoGenerationQueueName { get; init; } = "memo-generation";
}
```

**Step 2: Rewrite QueueService**

Replace entire `QueueService.cs` with:
```csharp
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class QueueService : IQueueService
{
    private readonly QueueClient _transcriptionQueue;
    private readonly QueueClient _memoQueue;
    private bool _ensured;

    public QueueService(IOptions<StorageQueueOptions> options)
    {
        var opts = options.Value;
        _transcriptionQueue = new QueueClient(opts.ConnectionString, opts.TranscriptionQueueName);
        _memoQueue = new QueueClient(opts.ConnectionString, opts.MemoGenerationQueueName);
    }

    private async Task EnsureQueuesAsync()
    {
        if (_ensured) return;
        await _transcriptionQueue.CreateIfNotExistsAsync();
        await _memoQueue.CreateIfNotExistsAsync();
        _ensured = true;
    }

    public async Task SendTranscriptionJobAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language = "da")
    {
        await EnsureQueuesAsync();
        var body = JsonSerializer.Serialize(new { sessionId, chunkId, chunkIndex, blobPath, language });
        await _transcriptionQueue.SendMessageAsync(body);
    }

    public async Task SendMemoGenerationJobAsync(string sessionId)
    {
        await EnsureQueuesAsync();
        var body = JsonSerializer.Serialize(new { sessionId });
        await _memoQueue.SendMessageAsync(body);
    }
}
```

**Step 3: Delete NoOpQueueService**

Delete file: `src/MyMemo.Shared/Services/NoOpQueueService.cs`

---

### Task 3: Rewrite workers to use polling

**Files:**
- Modify: `src/MyMemo.Worker/Workers/TranscriptionWorker.cs`
- Modify: `src/MyMemo.Worker/Workers/MemoGenerationWorker.cs`

**Step 1: Rewrite TranscriptionWorker**

Replace the `TranscriptionWorker` class (keep `TranscriptionProcessor` unchanged) with:
```csharp
public sealed class TranscriptionWorker(
    IServiceProvider serviceProvider,
    IOptions<StorageQueueOptions> options,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = new QueueClient(options.Value.ConnectionString, options.Value.TranscriptionQueueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var response = await queue.ReceiveMessageAsync(cancellationToken: stoppingToken);
            if (response.Value is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            var message = response.Value;
            try
            {
                var body = JsonSerializer.Deserialize<TranscriptionJob>(message.MessageText)!;
                logger.LogInformation("Processing transcription for session {SessionId}, chunk {ChunkIndex}",
                    body.SessionId, body.ChunkIndex);

                using var scope = serviceProvider.CreateScope();
                var processor = new TranscriptionProcessor(
                    scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                    scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IBlobStorageService>(),
                    scope.ServiceProvider.GetRequiredService<IWhisperService>(),
                    scope.ServiceProvider.GetRequiredService<IQueueService>());

                await processor.ProcessAsync(body.SessionId, body.ChunkId, body.ChunkIndex, body.BlobPath, body.Language);
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing transcription message");
            }
        }
    }

    private sealed record TranscriptionJob(string SessionId, string ChunkId, int ChunkIndex, string BlobPath, string Language);
}
```

Update imports at top of file — replace:
```csharp
using Azure.Messaging.ServiceBus;
```
with:
```csharp
using Azure.Storage.Queues;
```

**Step 2: Rewrite MemoGenerationWorker**

Replace the `MemoGenerationWorker` class (keep `MemoGenerationProcessor` unchanged) with:
```csharp
public sealed class MemoGenerationWorker(
    IServiceProvider serviceProvider,
    IOptions<StorageQueueOptions> options,
    ILogger<MemoGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = new QueueClient(options.Value.ConnectionString, options.Value.MemoGenerationQueueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var response = await queue.ReceiveMessageAsync(cancellationToken: stoppingToken);
            if (response.Value is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            var message = response.Value;
            try
            {
                var body = JsonSerializer.Deserialize<MemoJob>(message.MessageText)!;
                logger.LogInformation("Processing memo generation for session {SessionId}", body.SessionId);

                using var scope = serviceProvider.CreateScope();
                var processor = new MemoGenerationProcessor(
                    scope.ServiceProvider.GetRequiredService<ISessionRepository>(),
                    scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IMemoRepository>(),
                    scope.ServiceProvider.GetRequiredService<IMemoGeneratorService>());

                await processor.ProcessAsync(body.SessionId);
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing memo generation message");
            }
        }
    }

    private sealed record MemoJob(string SessionId);
}
```

Update imports at top of file — replace:
```csharp
using Azure.Messaging.ServiceBus;
```
with:
```csharp
using Azure.Storage.Queues;
```

---

### Task 4: Update DI registration (API + Worker)

**Files:**
- Modify: `src/MyMemo.Api/Program.cs`
- Modify: `src/MyMemo.Worker/Program.cs`

**Step 1: Update API Program.cs**

Replace:
```csharp
builder.Services.Configure<AzureServiceBusOptions>(builder.Configuration.GetSection("AzureServiceBus"));
```
with:
```csharp
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
```

Replace the conditional registration block:
```csharp
var serviceBusConnectionString = builder.Configuration["AzureServiceBus:ConnectionString"];
if (string.IsNullOrEmpty(serviceBusConnectionString))
    builder.Services.AddSingleton<IQueueService, NoOpQueueService>();
else
    builder.Services.AddSingleton<IQueueService, QueueService>();
```
with:
```csharp
builder.Services.AddSingleton<IQueueService, QueueService>();
```

**Step 2: Update Worker Program.cs**

Replace:
```csharp
builder.Services.Configure<AzureServiceBusOptions>(builder.Configuration.GetSection("AzureServiceBus"));
```
with:
```csharp
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
```

---

### Task 5: Update config files

**Files:**
- Modify: `src/MyMemo.Api/appsettings.json`
- Modify: `src/MyMemo.Worker/appsettings.json`
- Modify: `.env`

**Step 1: Update API appsettings.json**

Replace:
```json
"AzureServiceBus": {
  "ConnectionString": "",
  "TranscriptionQueueName": "transcription-jobs",
  "MemoGenerationQueueName": "memo-generation"
},
```
with:
```json
"StorageQueue": {
  "ConnectionString": "",
  "TranscriptionQueueName": "transcription-jobs",
  "MemoGenerationQueueName": "memo-generation"
},
```

**Step 2: Update Worker appsettings.json**

Same replacement as step 1.

**Step 3: Update .env**

Replace:
```
AzureServiceBus__ConnectionString=
```
with:
```
StorageQueue__ConnectionString=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://azurite:10001/devstoreaccount1
```

Note: Uses port 10001 (Azurite queue endpoint) and the same dev account key as blob storage.

---

### Task 6: Run tests and verify

**Step 1: Run all tests**

Run: `cd /Users/kim/ReposV2/MyMemo/backend && dotnet test`
Expected: All tests pass. Existing tests mock `IQueueService` so the implementation change doesn't affect them.

**Step 2: Docker build and test**

Run: `cd /Users/kim/ReposV2/MyMemo/backend && docker compose up --build`
Expected: API and worker start without errors. Upload a chunk from the frontend — should get 202 and see worker log processing the transcription job.

---

### Task 7: Commit

```bash
git add -A
git commit -m "feat(backend): replace Service Bus with Storage Queues

Enables full local dev pipeline with Azurite. Simpler and cheaper
for production too, since the app only needs basic queue semantics."
```
