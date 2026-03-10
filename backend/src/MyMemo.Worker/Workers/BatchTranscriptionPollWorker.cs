using System.Text.Json;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class BatchTranscriptionPollWorker(
    IServiceProvider serviceProvider,
    ISpeechBatchTranscriptionService speechService,
    ILogger<BatchTranscriptionPollWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in batch transcription poll cycle");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PollPendingJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var batchJobs = scope.ServiceProvider.GetRequiredService<IBatchTranscriptionJobRepository>();
        var pendingJobs = await batchJobs.ListPendingAsync();

        if (pendingJobs.Count == 0) return;

        logger.LogInformation("Polling {Count} pending batch transcription jobs", pendingJobs.Count);

        foreach (var job in pendingJobs)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var status = await speechService.GetStatusAsync(job.AzureJobId);

                switch (status)
                {
                    case BatchTranscriptionStatus.Succeeded:
                        await HandleSucceededAsync(scope.ServiceProvider, job.Id, job.ChunkId, job.SessionId, job.AzureJobId);
                        break;
                    case BatchTranscriptionStatus.Failed:
                        await HandleFailedAsync(scope.ServiceProvider, job.Id, job.ChunkId, job.AzureJobId);
                        break;
                    default:
                        logger.LogDebug("Batch job {AzureJobId} still {Status}", job.AzureJobId, status);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling batch job {AzureJobId}", job.AzureJobId);
            }
        }
    }

    private async Task HandleSucceededAsync(IServiceProvider sp, string jobId, string chunkId, string sessionId, string azureJobId)
    {
        var batchJobs = sp.GetRequiredService<IBatchTranscriptionJobRepository>();
        var chunks = sp.GetRequiredService<IChunkRepository>();
        var transcriptions = sp.GetRequiredService<ITranscriptionRepository>();
        var memoTrigger = sp.GetRequiredService<IMemoTriggerService>();

        var result = await speechService.GetResultAsync(azureJobId);

        // Store diarization data as JSON in word_timestamps, readable text in raw_text
        var segmentsJson = JsonSerializer.Serialize(result.Segments.Select(s => new
        {
            speaker = s.SpeakerId,
            text = s.Text,
            start = s.StartSeconds,
            end = s.EndSeconds,
        }));

        await transcriptions.CreateAsync(chunkId, result.ReadableText, "da", null, segmentsJson);
        await chunks.UpdateStatusAsync(chunkId, "transcribed");
        await batchJobs.UpdateStatusAsync(jobId, "completed");

        logger.LogInformation("Batch transcription completed for chunk {ChunkId}, session {SessionId} ({SegmentCount} segments)",
            chunkId, sessionId, result.Segments.Count);

        await memoTrigger.TryQueueMemoGenerationAsync(sessionId);

        // Clean up the Azure job
        try { await speechService.DeleteAsync(azureJobId); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete Azure batch job {AzureJobId}", azureJobId); }
    }

    private async Task HandleFailedAsync(IServiceProvider sp, string jobId, string chunkId, string azureJobId)
    {
        var batchJobs = sp.GetRequiredService<IBatchTranscriptionJobRepository>();
        var chunks = sp.GetRequiredService<IChunkRepository>();

        await batchJobs.UpdateStatusAsync(jobId, "failed");
        await chunks.UpdateStatusAsync(chunkId, "failed", "Batch transcription failed");

        logger.LogError("Batch transcription failed for chunk {ChunkId}, Azure job {AzureJobId}", chunkId, azureJobId);

        try { await speechService.DeleteAsync(azureJobId); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete Azure batch job {AzureJobId}", azureJobId); }
    }
}
