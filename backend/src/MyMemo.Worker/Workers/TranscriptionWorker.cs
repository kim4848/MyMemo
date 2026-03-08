using System.Diagnostics;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class TranscriptionProcessor(
    IChunkRepository chunks,
    ITranscriptionRepository transcriptions,
    IBlobStorageService blobService,
    IWhisperService whisperService,
    IMemoTriggerService memoTrigger,
    ILogger<TranscriptionProcessor> logger)
{
    public async Task ProcessAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language)
    {
        try
        {
            await chunks.UpdateStatusAsync(chunkId, "transcribing");

            var sw = Stopwatch.StartNew();
            await using var audioStream = await blobService.DownloadAsync(blobPath);
            var result = await whisperService.TranscribeAsync(audioStream, language);
            sw.Stop();

            await transcriptions.CreateAsync(chunkId, result.Text, language, result.AverageConfidence, result.WordTimestampsJson, sw.ElapsedMilliseconds);
            await chunks.UpdateStatusAsync(chunkId, "transcribed");

            await memoTrigger.TryQueueMemoGenerationAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transcription failed for session {SessionId}, chunk {ChunkIndex} ({ChunkId})",
                sessionId, chunkIndex, chunkId);
            await chunks.UpdateStatusAsync(chunkId, "failed", ex.Message);
        }
    }
}

public sealed class TranscriptionWorker(
    IServiceProvider serviceProvider,
    IOptions<StorageQueueOptions> options,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    private const int MaxConcurrent = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = new QueueClient(options.Value.ConnectionString, options.Value.TranscriptionQueueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var responses = await queue.ReceiveMessagesAsync(MaxConcurrent, cancellationToken: stoppingToken);
            if (responses.Value.Length == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            var tasks = responses.Value.Select(message => ProcessMessageAsync(queue, message, stoppingToken));
            await Task.WhenAll(tasks);
        }
    }

    private const int MaxDequeueCount = 5;

    private async Task ProcessMessageAsync(QueueClient queue, Azure.Storage.Queues.Models.QueueMessage message, CancellationToken stoppingToken)
    {
        try
        {
            if (message.DequeueCount > MaxDequeueCount)
            {
                logger.LogError("Transcription message exceeded max retries ({MaxRetries}), deleting: {MessageText}",
                    MaxDequeueCount, message.MessageText);
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                return;
            }

            var body = JsonSerializer.Deserialize<TranscriptionJob>(message.MessageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            logger.LogInformation("Processing transcription for session {SessionId}, chunk {ChunkIndex} (attempt {Attempt})",
                body.SessionId, body.ChunkIndex, message.DequeueCount);

            using var scope = serviceProvider.CreateScope();
            var processor = new TranscriptionProcessor(
                scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                scope.ServiceProvider.GetRequiredService<IBlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<IWhisperService>(),
                scope.ServiceProvider.GetRequiredService<IMemoTriggerService>(),
                scope.ServiceProvider.GetRequiredService<ILogger<TranscriptionProcessor>>());

            await processor.ProcessAsync(body.SessionId, body.ChunkId, body.ChunkIndex, body.BlobPath, body.Language);
            await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing transcription message (attempt {Attempt}): {MessageText}",
                message.DequeueCount, message.MessageText);
            if (message.DequeueCount >= MaxDequeueCount)
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
        }
    }

    private sealed record TranscriptionJob(string SessionId, string ChunkId, int ChunkIndex, string BlobPath, string Language);
}
