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
    IAudioConverterService audioConverter,
    ISpeechBatchTranscriptionService speechService,
    IBatchTranscriptionJobRepository batchJobs,
    IMemoTriggerService memoTrigger,
    ILogger<TranscriptionProcessor> logger)
{
    public async Task ProcessAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language, string transcriptionMode)
    {
        if (transcriptionMode == "speech")
        {
            await ProcessSpeechAsync(sessionId, chunkId, chunkIndex, blobPath, language);
        }
        else
        {
            await ProcessWhisperAsync(sessionId, chunkId, chunkIndex, blobPath, language);
        }
    }

    private async Task ProcessWhisperAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language)
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
            logger.LogError(ex, "Whisper transcription failed for session {SessionId}, chunk {ChunkIndex} ({ChunkId})",
                sessionId, chunkIndex, chunkId);
            await chunks.UpdateStatusAsync(chunkId, "failed", ex.Message);
        }
    }

    private async Task ProcessSpeechAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language)
    {
        string? tempWavPath = null;
        try
        {
            await chunks.UpdateStatusAsync(chunkId, "transcribing");

            // 1. Download WebM from blob
            await using var audioStream = await blobService.DownloadAsync(blobPath);

            // 2. Convert to WAV via ffmpeg
            tempWavPath = await audioConverter.ConvertToWavAsync(audioStream);

            // 3. Upload WAV to blob
            var wavBlobPath = $"{blobPath}.wav";
            await using (var wavStream = File.OpenRead(tempWavPath))
                await blobService.UploadAsync(wavBlobPath, wavStream, "audio/wav");

            // 4. Generate SAS URL for WAV blob (24h expiry)
            var sasUrl = blobService.GenerateSasUrl(wavBlobPath, TimeSpan.FromHours(24));

            // 5. Submit to Azure Speech batch transcription
            var locale = language == "da" ? "da-DK" : language;
            var azureJobId = await speechService.SubmitAsync(sasUrl.ToString(), locale);

            // 6. Store batch job record
            var jobId = Guid.NewGuid().ToString("N");
            await batchJobs.CreateAsync(jobId, chunkId, sessionId, azureJobId);

            // 7. Update chunk status
            await chunks.UpdateStatusAsync(chunkId, "batch_submitted");

            logger.LogInformation("Submitted batch transcription for session {SessionId}, chunk {ChunkIndex}, Azure job {AzureJobId}",
                sessionId, chunkIndex, azureJobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Speech batch submission failed for session {SessionId}, chunk {ChunkIndex} ({ChunkId})",
                sessionId, chunkIndex, chunkId);
            await chunks.UpdateStatusAsync(chunkId, "failed", ex.Message);
        }
        finally
        {
            // 8. Delete temp WAV file
            if (tempWavPath != null && File.Exists(tempWavPath))
                File.Delete(tempWavPath);
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
            logger.LogInformation("Processing transcription for session {SessionId}, chunk {ChunkIndex} (attempt {Attempt}, mode {Mode})",
                body.SessionId, body.ChunkIndex, message.DequeueCount, body.TranscriptionMode);

            using var scope = serviceProvider.CreateScope();
            var processor = new TranscriptionProcessor(
                scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                scope.ServiceProvider.GetRequiredService<IBlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<IWhisperService>(),
                scope.ServiceProvider.GetRequiredService<IAudioConverterService>(),
                scope.ServiceProvider.GetRequiredService<ISpeechBatchTranscriptionService>(),
                scope.ServiceProvider.GetRequiredService<IBatchTranscriptionJobRepository>(),
                scope.ServiceProvider.GetRequiredService<IMemoTriggerService>(),
                scope.ServiceProvider.GetRequiredService<ILogger<TranscriptionProcessor>>());

            await processor.ProcessAsync(body.SessionId, body.ChunkId, body.ChunkIndex, body.BlobPath, body.Language, body.TranscriptionMode);
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

    private sealed record TranscriptionJob(string SessionId, string ChunkId, int ChunkIndex, string BlobPath, string Language, string TranscriptionMode = "whisper");
}
