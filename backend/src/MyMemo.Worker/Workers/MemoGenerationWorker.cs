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
    public async Task ProcessAsync(string sessionId)
    {
        try
        {
            var session = await sessions.GetByIdAsync(sessionId);
            if (session is null) return;

            // Idempotency: skip if memo already exists (handles double-queuing race)
            var existingMemo = await memos.GetBySessionIdAsync(sessionId);
            if (existingMemo is not null)
            {
                await sessions.UpdateStatusAsync(sessionId, "completed");
                return;
            }

            // Guard: ensure all chunks are transcribed before generating memo
            if (!await chunks.AreAllTranscribedAsync(sessionId))
            {
                logger.LogWarning("Not all chunks transcribed for session {SessionId}, skipping memo generation", sessionId);
                return;
            }

            var allTranscriptions = await transcriptions.ListBySessionAsync(sessionId);
            var fullText = string.Join("\n\n", allTranscriptions.Select(t => t.RawText));

            var result = await memoGenerator.GenerateAsync(fullText, session.OutputMode);

            await memos.CreateAsync(sessionId, session.OutputMode, result.Content, result.ModelUsed, result.PromptTokens, result.CompletionTokens);
            await sessions.UpdateStatusAsync(sessionId, "completed");
        }
        catch (Exception ex)
        {
            // Handle race condition: another job may have created the memo
            var existingMemo = await memos.GetBySessionIdAsync(sessionId);
            if (existingMemo is not null)
            {
                await sessions.UpdateStatusAsync(sessionId, "completed");
                return;
            }

            logger.LogError(ex, "Memo generation failed for session {SessionId}", sessionId);
            await sessions.UpdateStatusAsync(sessionId, "failed");
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

            var message = response.Value;
            try
            {
                var body = JsonSerializer.Deserialize<MemoJob>(message.MessageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                logger.LogInformation("Processing memo generation for session {SessionId}", body.SessionId);

                using var scope = serviceProvider.CreateScope();
                var processor = new MemoGenerationProcessor(
                    scope.ServiceProvider.GetRequiredService<ISessionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                    scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IMemoRepository>(),
                    scope.ServiceProvider.GetRequiredService<IMemoGeneratorService>(),
                    scope.ServiceProvider.GetRequiredService<ILogger<MemoGenerationProcessor>>());

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
