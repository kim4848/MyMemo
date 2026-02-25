using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ITranscriptionRepository
{
    Task CreateAsync(string chunkId, string rawText, string language, double? confidence, string? wordTimestamps);
    Task<IReadOnlyList<Transcription>> ListBySessionAsync(string sessionId);
}
