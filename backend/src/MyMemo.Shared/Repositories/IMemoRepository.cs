using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IMemoRepository
{
    Task CreateAsync(string sessionId, string outputMode, string content, string modelUsed, int? promptTokens, int? completionTokens, long? generationDurationMs = null);
    Task<Memo?> GetBySessionIdAsync(string sessionId);
    Task DeleteBySessionIdAsync(string sessionId);
}
