using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class InfographicRepository(IDbConnectionFactory db) : IInfographicRepository
{
    public async Task CreateAsync(string sessionId, string imageContent, string modelUsed, int? promptTokens, int? completionTokens, long? generationDurationMs = null)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO infographics (id, session_id, image_content, model_used, prompt_tokens, completion_tokens, generation_duration_ms, created_at)
            VALUES (@id, @sessionId, @imageContent, @modelUsed, @promptTokens, @completionTokens, @generationDurationMs, @now)
            """,
            new { id, sessionId, imageContent, modelUsed, promptTokens, completionTokens, generationDurationMs, now });
    }

    public async Task<Infographic?> GetBySessionIdAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Infographic>(
            """
            SELECT id AS Id, session_id AS SessionId, image_content AS ImageContent,
                   model_used AS ModelUsed, prompt_tokens AS PromptTokens,
                   completion_tokens AS CompletionTokens, generation_duration_ms AS GenerationDurationMs,
                   created_at AS CreatedAt
            FROM infographics WHERE session_id = @sessionId
            """,
            new { sessionId });
    }

    public async Task DeleteBySessionIdAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM infographics WHERE session_id = @sessionId", new { sessionId });
    }
}
