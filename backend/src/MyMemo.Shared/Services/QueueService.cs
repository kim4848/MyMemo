using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class QueueService : IQueueService, IAsyncDisposable
{
    private readonly ServiceBusSender _transcriptionSender;
    private readonly ServiceBusSender _memoSender;
    private readonly ServiceBusClient _client;

    public QueueService(IOptions<AzureServiceBusOptions> options)
    {
        _client = new ServiceBusClient(options.Value.ConnectionString);
        _transcriptionSender = _client.CreateSender(options.Value.TranscriptionQueueName);
        _memoSender = _client.CreateSender(options.Value.MemoGenerationQueueName);
    }

    public async Task SendTranscriptionJobAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language = "da")
    {
        var body = JsonSerializer.Serialize(new { sessionId, chunkId, chunkIndex, blobPath, language });
        await _transcriptionSender.SendMessageAsync(new ServiceBusMessage(body));
    }

    public async Task SendMemoGenerationJobAsync(string sessionId)
    {
        var body = JsonSerializer.Serialize(new { sessionId });
        await _memoSender.SendMessageAsync(new ServiceBusMessage(body));
    }

    public async ValueTask DisposeAsync()
    {
        await _transcriptionSender.DisposeAsync();
        await _memoSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
