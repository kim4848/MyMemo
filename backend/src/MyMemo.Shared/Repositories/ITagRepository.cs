using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ITagRepository
{
    Task<Tag> CreateAsync(string userId, string name, string? color = null);
    Task<IReadOnlyList<Tag>> ListByUserAsync(string userId);
    Task DeleteAsync(string id);
    Task UpdateAsync(string id, string name, string? color);
    Task AddTagToSessionAsync(string sessionId, string tagId);
    Task RemoveTagFromSessionAsync(string sessionId, string tagId);
    Task<IReadOnlyList<Tag>> GetTagsForSessionAsync(string sessionId);
    Task<IDictionary<string, List<Tag>>> GetTagsForSessionsAsync(IEnumerable<string> sessionIds);
}
