using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class BatchTranscriptionJobRepository(IDbConnectionFactory db) : IBatchTranscriptionJobRepository
{
    public async Task CreateAsync(string id, string chunkId, string sessionId, string azureJobId)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO batch_transcription_jobs (id, chunk_id, session_id, azure_job_id, status, created_at)
            VALUES (@id, @chunkId, @sessionId, @azureJobId, 'submitted', @now)
            """,
            new { id, chunkId, sessionId, azureJobId, now });
    }

    public async Task<IReadOnlyList<BatchTranscriptionJob>> ListPendingAsync()
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<BatchTranscriptionJob>(
            """
            SELECT id AS Id, chunk_id AS ChunkId, session_id AS SessionId,
                   azure_job_id AS AzureJobId, status AS Status,
                   created_at AS CreatedAt, completed_at AS CompletedAt
            FROM batch_transcription_jobs
            WHERE status = 'submitted'
            """);
        return results.ToList();
    }

    public async Task UpdateStatusAsync(string id, string status)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE batch_transcription_jobs SET status = @status, completed_at = @now WHERE id = @id",
            new { id, status, now });
    }
}
