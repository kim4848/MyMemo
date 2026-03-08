using System.Diagnostics;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class MemoGenerationProcessor(
    ISessionRepository sessions,
    IChunkRepository chunks,
    ITranscriptionRepository transcriptions,
    IMemoRepository memos,
    IMemoGeneratorService memoGenerator,
    ILogger<MemoGenerationProcessor> logger)
{
    /// <returns>true if the job was handled (success, duplicate, or permanent skip); false if it should be retried.</returns>
    public async Task<bool> ProcessAsync(string sessionId)
    {
        try
        {
            var session = await sessions.GetByIdAsync(sessionId);
            if (session is null) return true;

            // Never generate while still recording
            if (session.Status == "recording")
            {
                logger.LogInformation("Session {SessionId} still recording, will retry", sessionId);
                return false;
            }

            // Idempotency: skip if memo already exists (handles double-queuing race)
            var existingMemo = await memos.GetBySessionIdAsync(sessionId);
            if (existingMemo is not null)
            {
                await sessions.UpdateStatusAsync(sessionId, "completed");
                return true;
            }

            // Guard: ensure session has chunks and all are transcribed (single query)
            var (chunkCount, allTranscribed) = await chunks.GetTranscriptionStatusAsync(sessionId);
            if (chunkCount == 0)
            {
                logger.LogWarning("Session {SessionId} has zero chunks, skipping memo generation", sessionId);
                return true;
            }

            if (!allTranscribed)
            {
                logger.LogInformation("Not all chunks transcribed for session {SessionId}, will retry", sessionId);
                return false;
            }

            var allTranscriptions = await transcriptions.ListBySessionAsync(sessionId);
            var fullText = string.Join("\n\n", allTranscriptions.Select(t => t.RawText));

            var sw = Stopwatch.StartNew();
            var result = await memoGenerator.GenerateAsync(fullText, session.OutputMode, session.Context);
            sw.Stop();

            await memos.CreateAsync(sessionId, session.OutputMode, result.Content, result.ModelUsed, result.PromptTokens, result.CompletionTokens, sw.ElapsedMilliseconds);
            await sessions.UpdateStatusAsync(sessionId, "completed");
            return true;
        }
        catch (Exception ex)
        {
            // Handle race condition: another job may have created the memo
            var existingMemo = await memos.GetBySessionIdAsync(sessionId);
            if (existingMemo is not null)
            {
                await sessions.UpdateStatusAsync(sessionId, "completed");
                return true;
            }

            logger.LogError(ex, "Memo generation failed for session {SessionId}", sessionId);
            await sessions.UpdateStatusAsync(sessionId, "failed");
            return true;
        }
    }
}

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

            const int maxDequeueCount = 5;
            var message = response.Value;
            try
            {
                if (message.DequeueCount > maxDequeueCount)
                {
                    logger.LogError("Memo generation message exceeded max retries ({MaxRetries}), deleting: {MessageText}",
                        maxDequeueCount, message.MessageText);
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                    continue;
                }

                var body = JsonSerializer.Deserialize<MemoJob>(message.MessageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                logger.LogInformation("Processing memo generation for session {SessionId} (attempt {Attempt})",
                    body.SessionId, message.DequeueCount);

                using var scope = serviceProvider.CreateScope();
                var processor = new MemoGenerationProcessor(
                    scope.ServiceProvider.GetRequiredService<ISessionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                    scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IMemoRepository>(),
                    scope.ServiceProvider.GetRequiredService<IMemoGeneratorService>(),
                    scope.ServiceProvider.GetRequiredService<ILogger<MemoGenerationProcessor>>());

                var handled = await processor.ProcessAsync(body.SessionId);
                if (handled)
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing memo generation message (attempt {Attempt}): {MessageText}",
                    message.DequeueCount, message.MessageText);
                if (message.DequeueCount >= maxDequeueCount)
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
        }
    }

    private sealed record MemoJob(string SessionId);
}
