using Dapper;

namespace MyMemo.Shared.Database;

public static class DatabaseInitializer
{
    public static async Task Initialize(IDbConnectionFactory factory)
    {
        using var connection = await factory.CreateConnectionAsync();
        await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await connection.ExecuteAsync("PRAGMA foreign_keys=ON;");

        var schema = GetEmbeddedSchema();
        await connection.ExecuteAsync(schema);

        // Idempotent migrations: ALTER TABLE throws if column already exists
        string[] migrations =
        [
            "ALTER TABLE transcriptions ADD COLUMN transcription_duration_ms INTEGER",
            "ALTER TABLE memos ADD COLUMN generation_duration_ms INTEGER",
            "ALTER TABLE sessions ADD COLUMN memo_queued INTEGER NOT NULL DEFAULT 0",
        ];
        foreach (var sql in migrations)
        {
            try { await connection.ExecuteAsync(sql); }
            catch { /* Column already exists — ignore */ }
        }
    }

    private static string GetEmbeddedSchema()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream("MyMemo.Shared.Database.schema.sql")
            ?? throw new InvalidOperationException("schema.sql not found as embedded resource");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
