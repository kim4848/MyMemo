using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class QueueService : IQueueService
{
    private readonly QueueClient _transcriptionQueue;
    private readonly QueueClient _memoQueue;
    private readonly QueueClient _infographicQueue;
    private bool _ensured;

    public QueueService(IOptions<StorageQueueOptions> options)
    {
        var opts = options.Value;
        _transcriptionQueue = new QueueClient(opts.ConnectionString, opts.TranscriptionQueueName);
        _memoQueue = new QueueClient(opts.ConnectionString, opts.MemoGenerationQueueName);
        _infographicQueue = new QueueClient(opts.ConnectionString, opts.InfographicGenerationQueueName);
    }

    private async Task EnsureQueuesAsync()
    {
        if (_ensured) return;
        await _transcriptionQueue.CreateIfNotExistsAsync();
        await _memoQueue.CreateIfNotExistsAsync();
        await _infographicQueue.CreateIfNotExistsAsync();
        _ensured = true;
    }

    public async Task SendTranscriptionJobAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language = "da")
    {
        await EnsureQueuesAsync();
        var body = JsonSerializer.Serialize(new { sessionId, chunkId, chunkIndex, blobPath, language });
        await _transcriptionQueue.SendMessageAsync(body);
    }

    public async Task SendMemoGenerationJobAsync(string sessionId)
    {
        await EnsureQueuesAsync();
        var body = JsonSerializer.Serialize(new { sessionId });
        await _memoQueue.SendMessageAsync(body);
    }

    public async Task SendInfographicGenerationJobAsync(string sessionId)
    {
        await EnsureQueuesAsync();
        var body = JsonSerializer.Serialize(new { sessionId });
        await _infographicQueue.SendMessageAsync(body);
    }
}
