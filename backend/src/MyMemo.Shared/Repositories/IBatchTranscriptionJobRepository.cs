using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IBatchTranscriptionJobRepository
{
    Task CreateAsync(string id, string chunkId, string sessionId, string azureJobId);
    Task<IReadOnlyList<BatchTranscriptionJob>> ListPendingAsync();
    Task UpdateStatusAsync(string id, string status);
}
