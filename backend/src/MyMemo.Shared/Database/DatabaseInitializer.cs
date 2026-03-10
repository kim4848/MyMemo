using Dapper;

namespace MyMemo.Shared.Database;

public static class DatabaseInitializer
{
    public static async Task Initialize(IDbConnectionFactory factory)
    {
        using var connection = await factory.CreateConnectionAsync();
        try { await connection.ExecuteAsync("PRAGMA journal_mode=WAL;"); } catch { /* not supported on Turso */ }
        try { await connection.ExecuteAsync("PRAGMA foreign_keys=ON;"); } catch { /* not supported on Turso */ }

        var schema = GetEmbeddedSchema();
        foreach (var stmt in SplitStatements(schema))
            await connection.ExecuteAsync(stmt);

        // Idempotent migrations: ALTER TABLE throws if column already exists
        string[] migrations =
        [
            "ALTER TABLE transcriptions ADD COLUMN transcription_duration_ms INTEGER",
            "ALTER TABLE memos ADD COLUMN generation_duration_ms INTEGER",
            "ALTER TABLE sessions ADD COLUMN memo_queued INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE sessions ADD COLUMN context TEXT",
            "ALTER TABLE infographics ADD COLUMN image_content TEXT",
            "ALTER TABLE transcriptions ADD COLUMN transcription_provider TEXT DEFAULT 'whisper'",
        ];
        foreach (var sql in migrations)
        {
            try { await connection.ExecuteAsync(sql); }
            catch { /* Column already exists — ignore */ }
        }
    }

    private static IEnumerable<string> SplitStatements(string sql) =>
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Where(s => s.Length > 0);

    private static string GetEmbeddedSchema()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream("MyMemo.Shared.Database.schema.sql")
            ?? throw new InvalidOperationException("schema.sql not found as embedded resource");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
