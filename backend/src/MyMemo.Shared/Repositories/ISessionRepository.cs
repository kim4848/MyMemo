using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ISessionRepository
{
    Task<Session> CreateAsync(string userId, string outputMode, string audioSource, string? context = null, string transcriptionMode = "whisper");
    Task<IReadOnlyList<Session>> ListByUserAsync(string userId);
    Task<Session?> GetByIdAsync(string id);
    Task DeleteAsync(string id);
    Task UpdateStatusAsync(string id, string status);
    Task SetEndedAtAsync(string id);
    Task<bool> IsFinalizedAsync(string id);
    Task UpdateOutputModeAsync(string id, string outputMode);
    Task UpdateContextAsync(string id, string? context);
    Task<bool> TrySetMemoQueuedAsync(string id);
}
