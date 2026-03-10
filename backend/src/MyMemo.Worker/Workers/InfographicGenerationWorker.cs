using System.Diagnostics;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class InfographicGenerationProcessor(
    IMemoRepository memos,
    IInfographicRepository infographics,
    IInfographicService infographicService,
    ILogger<InfographicGenerationProcessor> logger)
{
    /// <returns>true if the job was handled (success, duplicate, or permanent skip); false if it should be retried.</returns>
    public async Task<bool> ProcessAsync(string sessionId)
    {
        try
        {
            // Idempotency: skip if infographic already exists
            var existing = await infographics.GetBySessionIdAsync(sessionId);
            if (existing is not null)
                return true;

            var memo = await memos.GetBySessionIdAsync(sessionId);
            if (memo is null)
            {
                logger.LogWarning("Session {SessionId} has no memo, skipping infographic generation", sessionId);
                return true;
            }

            var sw = Stopwatch.StartNew();
            var result = await infographicService.GenerateAsync(memo.Content, memo.OutputMode);
            sw.Stop();

            await infographics.CreateAsync(
                sessionId,
                result.ImageBase64,
                result.ModelUsed,
                result.PromptTokens,
                result.CompletionTokens,
                sw.ElapsedMilliseconds);

            logger.LogInformation("Infographic generated for session {SessionId} in {Duration}ms", sessionId, sw.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            // Handle race condition: another job may have created the infographic
            var existing = await infographics.GetBySessionIdAsync(sessionId);
            if (existing is not null)
                return true;

            logger.LogError(ex, "Infographic generation failed for session {SessionId}", sessionId);
            return false;
        }
    }
}

public sealed class InfographicGenerationWorker(
    IServiceProvider serviceProvider,
    IOptions<StorageQueueOptions> options,
    ILogger<InfographicGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = new QueueClient(options.Value.ConnectionString, options.Value.InfographicGenerationQueueName);
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
                    logger.LogError("Infographic generation message exceeded max retries ({MaxRetries}), deleting: {MessageText}",
                        maxDequeueCount, message.MessageText);
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                    continue;
                }

                var body = JsonSerializer.Deserialize<InfographicJob>(message.MessageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                logger.LogInformation("Processing infographic generation for session {SessionId} (attempt {Attempt})",
                    body.SessionId, message.DequeueCount);

                using var scope = serviceProvider.CreateScope();
                var processor = new InfographicGenerationProcessor(
                    scope.ServiceProvider.GetRequiredService<IMemoRepository>(),
                    scope.ServiceProvider.GetRequiredService<IInfographicRepository>(),
                    scope.ServiceProvider.GetRequiredService<IInfographicService>(),
                    scope.ServiceProvider.GetRequiredService<ILogger<InfographicGenerationProcessor>>());

                var handled = await processor.ProcessAsync(body.SessionId);
                if (handled)
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing infographic generation message (attempt {Attempt}): {MessageText}",
                    message.DequeueCount, message.MessageText);
                if (message.DequeueCount >= maxDequeueCount)
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
        }
    }

    private sealed record InfographicJob(string SessionId);
}
