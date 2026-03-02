# Replace Azure Service Bus with Azure Storage Queues

## Context

The app uses two queues for async job processing:
- `transcription-jobs` — API enqueues after chunk upload, worker transcribes via Whisper
- `memo-generation` — worker enqueues after all chunks transcribed, generates memo via GPT

Azure Service Bus is overkill for this simple fire-and-forget pattern. Storage Queues work with Azurite locally and are cheaper in production.

## Changes

### Shared library (`MyMemo.Shared`)
- **NuGet**: Remove `Azure.Messaging.ServiceBus`, add `Azure.Storage.Queues`
- **Config**: New `StorageQueueOptions` with `ConnectionString`, `TranscriptionQueueName`, `MemoGenerationQueueName`. Remove `AzureServiceBusOptions`.
- **QueueService**: Replace `ServiceBusClient`/`ServiceBusSender` with two `QueueClient` instances. Auto-create queues on first use. Messages are base64-encoded JSON.
- **Delete**: `NoOpQueueService`

### API (`MyMemo.Api`)
- **Program.cs**: Register `QueueService` unconditionally (works with Azurite). Configure `StorageQueueOptions`. Remove conditional NoOp registration.

### Worker (`MyMemo.Worker`)
- **TranscriptionWorker**: Replace `ServiceBusProcessor` event model with polling loop using `QueueClient.ReceiveMessagesAsync`. Delete message after successful processing.
- **MemoGenerationWorker**: Same polling pattern.
- **Program.cs**: Update DI registration for new config/options.

### Config
- `.env`: Remove `AzureServiceBus__ConnectionString`. Add `StorageQueue__ConnectionString` pointing to Azurite (same connection string as blob storage).
- `appsettings.json` (both API and Worker): Update config section names.
- `docker-compose.yml`: No changes needed (Azurite already exposes queue port 10001).
