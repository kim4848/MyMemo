using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class TranscriptionProcessor(
    IChunkRepository chunks,
    ITranscriptionRepository transcriptions,
    IBlobStorageService blobService,
    IWhisperService whisperService,
    IQueueService queueService)
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
            await chunks.UpdateStatusAsync(chunkId, "failed", ex.Message);
        }
    }
}

public sealed class TranscriptionWorker(
    IServiceProvider serviceProvider,
    IOptions<AzureServiceBusOptions> options,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = new ServiceBusClient(options.Value.ConnectionString);
        var processor = client.CreateProcessor(options.Value.TranscriptionQueueName);

        processor.ProcessMessageAsync += async args =>
        {
            var body = JsonSerializer.Deserialize<TranscriptionJob>(args.Message.Body.ToString())!;
            logger.LogInformation("Processing transcription job for session {SessionId}, chunk {ChunkIndex}", body.SessionId, body.ChunkIndex);

            using var scope = serviceProvider.CreateScope();
            var transcriptionProcessor = new TranscriptionProcessor(
                scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                scope.ServiceProvider.GetRequiredService<IBlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<IWhisperService>(),
                scope.ServiceProvider.GetRequiredService<IQueueService>());

            await transcriptionProcessor.ProcessAsync(body.SessionId, body.ChunkId, body.ChunkIndex, body.BlobPath, body.Language);
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus processing error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync();
    }

    private sealed record TranscriptionJob(string SessionId, string ChunkId, int ChunkIndex, string BlobPath, string Language);
}
