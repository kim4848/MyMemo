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
    IQueueService queueService,
    ILogger<TranscriptionProcessor> logger)
{
    public async Task ProcessAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language)
    {
        try
        {
            await chunks.UpdateStatusAsync(chunkId, "transcribing");

            await using var audioStream = await blobService.DownloadAsync(blobPath);
            var result = await whisperService.TranscribeAsync(audioStream, language);

            await transcriptions.CreateAsync(chunkId, result.Text, language, result.AverageConfidence, result.WordTimestampsJson);
            await chunks.UpdateStatusAsync(chunkId, "transcribed");

            if (await chunks.AreAllTranscribedAsync(sessionId))
            {
                await queueService.SendMemoGenerationJobAsync(sessionId);
            }
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
                var body = JsonSerializer.Deserialize<TranscriptionJob>(message.MessageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                logger.LogInformation("Processing transcription for session {SessionId}, chunk {ChunkIndex}",
                    body.SessionId, body.ChunkIndex);

                using var scope = serviceProvider.CreateScope();
                var processor = new TranscriptionProcessor(
                    scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                    scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IBlobStorageService>(),
                    scope.ServiceProvider.GetRequiredService<IWhisperService>(),
                    scope.ServiceProvider.GetRequiredService<IQueueService>(),
                    scope.ServiceProvider.GetRequiredService<ILogger<TranscriptionProcessor>>());

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
