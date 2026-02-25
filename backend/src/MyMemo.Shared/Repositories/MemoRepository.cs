using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class MemoRepository(IDbConnectionFactory db) : IMemoRepository
{
    public async Task CreateAsync(string sessionId, string outputMode, string content, string modelUsed, int? promptTokens, int? completionTokens)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO memos (id, session_id, output_mode, content, model_used, prompt_tokens, completion_tokens, created_at)
            VALUES (@id, @sessionId, @outputMode, @content, @modelUsed, @promptTokens, @completionTokens, @now)
            """,
            new { id, sessionId, outputMode, content, modelUsed, promptTokens, completionTokens, now });
    }

    public async Task<Memo?> GetBySessionIdAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Memo>(
            """
            SELECT id AS Id, session_id AS SessionId, output_mode AS OutputMode, content AS Content,
                   model_used AS ModelUsed, prompt_tokens AS PromptTokens,
                   completion_tokens AS CompletionTokens, created_at AS CreatedAt
            FROM memos WHERE session_id = @sessionId
            """,
            new { sessionId });
    }
}
