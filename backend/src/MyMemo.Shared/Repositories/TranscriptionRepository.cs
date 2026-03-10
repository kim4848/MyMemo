using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class TranscriptionRepository(IDbConnectionFactory db) : ITranscriptionRepository
{
    public async Task CreateAsync(string chunkId, string rawText, string language, double? confidence, string? wordTimestamps, long? transcriptionDurationMs = null, string? transcriptionProvider = null)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var provider = transcriptionProvider ?? "whisper";
        await conn.ExecuteAsync(
            """
            INSERT INTO transcriptions (id, chunk_id, raw_text, language, confidence, word_timestamps, transcription_duration_ms, transcription_provider, created_at)
            VALUES (@id, @chunkId, @rawText, @language, @confidence, @wordTimestamps, @transcriptionDurationMs, @provider, @now)
            """,
            new { id, chunkId, rawText, language, confidence, wordTimestamps, transcriptionDurationMs, provider, now });
    }

    public async Task<IReadOnlyList<Transcription>> ListBySessionAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Transcription>(
            """
            SELECT t.id AS Id, t.chunk_id AS ChunkId, t.raw_text AS RawText, t.language AS Language,
                   t.confidence AS Confidence, t.word_timestamps AS WordTimestamps,
                   t.transcription_duration_ms AS TranscriptionDurationMs,
                   t.transcription_provider AS TranscriptionProvider, t.created_at AS CreatedAt
            FROM transcriptions t
            INNER JOIN chunks c ON t.chunk_id = c.id
            WHERE c.session_id = @sessionId
            ORDER BY c.chunk_index
            """,
            new { sessionId });
        return results.ToList();
    }
}
