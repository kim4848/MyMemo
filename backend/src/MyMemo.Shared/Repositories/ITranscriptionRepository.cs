using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ITranscriptionRepository
{
    Task CreateAsync(string chunkId, string rawText, string language, double? confidence, string? wordTimestamps, long? transcriptionDurationMs = null);
    Task<IReadOnlyList<Transcription>> ListBySessionAsync(string sessionId);
    Task ReplaceSpeakerInSessionAsync(string sessionId, string oldName, string newName);
}
