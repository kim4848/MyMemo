using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class MemoRepository(IDbConnectionFactory db) : IMemoRepository
{
    public async Task CreateAsync(string sessionId, string outputMode, string content, string modelUsed, int? promptTokens, int? completionTokens, long? generationDurationMs = null)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO memos (id, session_id, output_mode, content, model_used, prompt_tokens, completion_tokens, generation_duration_ms, created_at)
            VALUES (@id, @sessionId, @outputMode, @content, @modelUsed, @promptTokens, @completionTokens, @generationDurationMs, @now)
            """,
            new { id, sessionId, outputMode, content, modelUsed, promptTokens, completionTokens, generationDurationMs, now });
    }

    public async Task<Memo?> GetBySessionIdAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Memo>(
            """
            SELECT id AS Id, session_id AS SessionId, output_mode AS OutputMode, content AS Content,
                   model_used AS ModelUsed, prompt_tokens AS PromptTokens,
                   completion_tokens AS CompletionTokens, generation_duration_ms AS GenerationDurationMs,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM memos WHERE session_id = @sessionId
            """,
            new { sessionId });
    }

    public async Task UpdateContentAsync(string sessionId, string content)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE memos SET content = @content, updated_at = @now WHERE session_id = @sessionId",
            new { content, now, sessionId });
    }

    public async Task DeleteBySessionIdAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM memos WHERE session_id = @sessionId", new { sessionId });
    }
}
