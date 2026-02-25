using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class MemoGenerationProcessor(
    ISessionRepository sessions,
    ITranscriptionRepository transcriptions,
    IMemoRepository memos,
    IMemoGeneratorService memoGenerator)
{
    public async Task ProcessAsync(string sessionId)
    {
        try
        {
            var session = await sessions.GetByIdAsync(sessionId);
            if (session is null) return;

            var allTranscriptions = await transcriptions.ListBySessionAsync(sessionId);
            var fullText = string.Join("\n\n", allTranscriptions.Select(t => t.RawText));

            var result = await memoGenerator.GenerateAsync(fullText, session.OutputMode);

            await memos.CreateAsync(sessionId, session.OutputMode, result.Content, result.ModelUsed, result.PromptTokens, result.CompletionTokens);
            await sessions.UpdateStatusAsync(sessionId, "completed");
        }
        catch (Exception)
        {
            await sessions.UpdateStatusAsync(sessionId, "failed");
        }
    }
}

public sealed class MemoGenerationWorker(
    IServiceProvider serviceProvider,
    IOptions<AzureServiceBusOptions> options,
    ILogger<MemoGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = new ServiceBusClient(options.Value.ConnectionString);
        var processor = client.CreateProcessor(options.Value.MemoGenerationQueueName);

        processor.ProcessMessageAsync += async args =>
        {
            var body = JsonSerializer.Deserialize<MemoJob>(args.Message.Body.ToString())!;
            logger.LogInformation("Processing memo generation for session {SessionId}", body.SessionId);

            using var scope = serviceProvider.CreateScope();
            var memoProcessor = new MemoGenerationProcessor(
                scope.ServiceProvider.GetRequiredService<ISessionRepository>(),
                scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                scope.ServiceProvider.GetRequiredService<IMemoRepository>(),
                scope.ServiceProvider.GetRequiredService<IMemoGeneratorService>());

            await memoProcessor.ProcessAsync(body.SessionId);
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

    private sealed record MemoJob(string SessionId);
}
