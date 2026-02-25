using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class ChunkRepository(IDbConnectionFactory db) : IChunkRepository
{
    private const string SelectColumns =
        """
        id AS Id, session_id AS SessionId, chunk_index AS ChunkIndex,
        blob_path AS BlobPath, duration_sec AS DurationSec, status AS Status,
        error_message AS ErrorMessage, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    public async Task<Chunk> CreateAsync(string sessionId, int chunkIndex, string blobPath)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO chunks (id, session_id, chunk_index, blob_path, status, created_at, updated_at)
            VALUES (@id, @sessionId, @chunkIndex, @blobPath, 'uploaded', @now, @now)
            """,
            new { id, sessionId, chunkIndex, blobPath, now });

        return (await GetByIdAsync(id))!;
    }

    public async Task<Chunk?> GetByIdAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Chunk>(
            $"SELECT {SelectColumns} FROM chunks WHERE id = @id",
            new { id });
    }

    public async Task<IReadOnlyList<Chunk>> ListBySessionAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Chunk>(
            $"SELECT {SelectColumns} FROM chunks WHERE session_id = @sessionId ORDER BY chunk_index",
            new { sessionId });
        return results.ToList();
    }

    public async Task UpdateStatusAsync(string id, string status, string? errorMessage = null)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE chunks SET status = @status, error_message = @errorMessage, updated_at = @now WHERE id = @id",
            new { id, status, errorMessage, now });
    }

    public async Task<bool> AreAllTranscribedAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM chunks WHERE session_id = @sessionId AND status != 'transcribed'",
            new { sessionId });
        return count == 0;
    }
}
