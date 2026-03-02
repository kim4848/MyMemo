using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IChunkRepository
{
    Task<Chunk> CreateAsync(string sessionId, int chunkIndex, string blobPath);
    Task<Chunk?> GetByIdAsync(string id);
    Task<IReadOnlyList<Chunk>> ListBySessionAsync(string sessionId);
    Task UpdateStatusAsync(string id, string status, string? errorMessage = null);
    Task<bool> AreAllTranscribedAsync(string sessionId);
    Task<int> CountBySessionAsync(string sessionId);
    Task<(int Count, bool AllTranscribed)> GetTranscriptionStatusAsync(string sessionId);
}
