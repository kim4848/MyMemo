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
        context AS Context, transcription_mode AS TranscriptionMode, memo_queued AS MemoQueued,
        started_at AS StartedAt, ended_at AS EndedAt,
        created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<Session> CreateAsync(string userId, string outputMode, string audioSource, string? context = null, string transcriptionMode = "whisper")
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO sessions (id, user_id, status, output_mode, audio_source, context, transcription_mode, started_at, created_at, updated_at)
            VALUES (@id, @userId, 'recording', @outputMode, @audioSource, @context, @transcriptionMode, @now, @now, @now)
            """,
            new { id, userId, outputMode, audioSource, context, transcriptionMode, now });

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

    public async Task<bool> IsFinalizedAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sessions WHERE id = @id AND ended_at IS NOT NULL",
            new { id });
        return count > 0;
    }

    public async Task UpdateOutputModeAsync(string id, string outputMode)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET output_mode = @outputMode, updated_at = @now WHERE id = @id",
            new { id, outputMode, now });
    }

    public async Task UpdateContextAsync(string id, string? context)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET context = @context, updated_at = @now WHERE id = @id",
            new { id, context, now });
    }

    public async Task UpdateTitleAsync(string id, string title)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET title = @title, updated_at = @now WHERE id = @id",
            new { id, title, now });
    }

    public async Task<bool> TrySetMemoQueuedAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var rowsAffected = await conn.ExecuteAsync(
            "UPDATE sessions SET memo_queued = 1, updated_at = @now WHERE id = @id AND memo_queued = 0",
            new { id, now });
        return rowsAffected > 0;
    }
}
