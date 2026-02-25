using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IMemoRepository
{
    Task CreateAsync(string sessionId, string outputMode, string content, string modelUsed, int? promptTokens, int? completionTokens);
    Task<Memo?> GetBySessionIdAsync(string sessionId);
}
