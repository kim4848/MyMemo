using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class SessionRepository(IDbConnectionFactory db) : ISessionRepository
{
    private const string SelectColumns =
        """
        id AS Id, user_id AS UserId, title AS Title, status AS Status,
        output_mode AS OutputMode, audio_source AS AudioSource,
        started_at AS StartedAt, ended_at AS EndedAt,
        created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<Session> CreateAsync(string userId, string outputMode, string audioSource)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO sessions (id, user_id, status, output_mode, audio_source, started_at, created_at, updated_at)
            VALUES (@id, @userId, 'recording', @outputMode, @audioSource, @now, @now, @now)
            """,
            new { id, userId, outputMode, audioSource, now });

        return (await GetByIdAsync(id))!;
    }

    public async Task<IReadOnlyList<Session>> ListByUserAsync(string userId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Session>(
            $"SELECT {SelectColumns} FROM sessions WHERE user_id = @userId ORDER BY created_at DESC",
            new { userId });
        return results.ToList();
    }

    public async Task<Session?> GetByIdAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Session>(
            $"SELECT {SelectColumns} FROM sessions WHERE id = @id",
            new { id });
    }

    public async Task DeleteAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM sessions WHERE id = @id", new { id });
    }

    public async Task UpdateStatusAsync(string id, string status)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET status = @status, updated_at = @now WHERE id = @id",
            new { id, status, now });
    }

    public async Task SetEndedAtAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET ended_at = @now, updated_at = @now WHERE id = @id",
            new { id, now });
    }
}
