using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IInfographicRepository
{
    Task CreateAsync(string sessionId, string imageContent, string imageFormat, string modelUsed, long? generationDurationMs = null);
    Task<Infographic?> GetBySessionIdAsync(string sessionId);
    Task DeleteBySessionIdAsync(string sessionId);
}
