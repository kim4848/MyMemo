using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ISessionRepository
{
    Task<Session> CreateAsync(string userId, string outputMode, string audioSource);
    Task<IReadOnlyList<Session>> ListByUserAsync(string userId);
    Task<Session?> GetByIdAsync(string id);
    Task DeleteAsync(string id);
    Task UpdateStatusAsync(string id, string status);
    Task SetEndedAtAsync(string id);
}
