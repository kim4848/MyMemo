using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class TagRepository(IDbConnectionFactory db) : ITagRepository
{
    public async Task<Tag> CreateAsync(string userId, string name, string? color = null)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO tags (id, user_id, name, color, created_at)
            VALUES (@id, @userId, @name, @color, @now)
            """,
            new { id, userId, name, color, now });

        return new Tag { Id = id, UserId = userId, Name = name, Color = color, CreatedAt = now };
    }

    public async Task<IReadOnlyList<Tag>> ListByUserAsync(string userId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Tag>(
            """
            SELECT id AS Id, user_id AS UserId, name AS Name, color AS Color, created_at AS CreatedAt
            FROM tags WHERE user_id = @userId ORDER BY name
            """,
            new { userId });
        return results.ToList();
    }

    public async Task DeleteAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM tags WHERE id = @id", new { id });
    }

    public async Task UpdateAsync(string id, string name, string? color)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE tags SET name = @name, color = @color WHERE id = @id",
            new { id, name, color });
    }

    public async Task AddTagToSessionAsync(string sessionId, string tagId)
    {
        using var conn = await db.CreateConnectionAsync();
        try
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO session_tags (session_id, tag_id)
                VALUES (@sessionId, @tagId)
                """,
                new { sessionId, tagId });
        }
        catch { /* Row already exists — desired state */ }
    }

    public async Task RemoveTagFromSessionAsync(string sessionId, string tagId)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM session_tags WHERE session_id = @sessionId AND tag_id = @tagId",
            new { sessionId, tagId });
    }

    public async Task<IReadOnlyList<Tag>> GetTagsForSessionAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Tag>(
            """
            SELECT t.id AS Id, t.user_id AS UserId, t.name AS Name, t.color AS Color, t.created_at AS CreatedAt
            FROM tags t
            INNER JOIN session_tags st ON st.tag_id = t.id
            WHERE st.session_id = @sessionId
            ORDER BY t.name
            """,
            new { sessionId });
        return results.ToList();
    }

    public async Task<IDictionary<string, List<Tag>>> GetTagsForSessionsAsync(IEnumerable<string> sessionIds)
    {
        var ids = sessionIds.ToList();
        if (ids.Count == 0) return new Dictionary<string, List<Tag>>();

        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<(string SessionId, string Id, string UserId, string Name, string? Color, string CreatedAt)>(
            """
            SELECT st.session_id AS SessionId, t.id AS Id, t.user_id AS UserId, t.name AS Name, t.color AS Color, t.created_at AS CreatedAt
            FROM tags t
            INNER JOIN session_tags st ON st.tag_id = t.id
            WHERE st.session_id IN @ids
            ORDER BY t.name
            """,
            new { ids });

        var dict = new Dictionary<string, List<Tag>>();
        foreach (var row in results)
        {
            if (!dict.TryGetValue(row.SessionId, out var list))
            {
                list = [];
                dict[row.SessionId] = list;
            }
            list.Add(new Tag { Id = row.Id, UserId = row.UserId, Name = row.Name, Color = row.Color, CreatedAt = row.CreatedAt });
        }
        return dict;
    }
}
