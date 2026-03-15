using Dapper;
using Microsoft.Data.SqlClient;

namespace MyMemo.Shared.Database;

public static class DatabaseInitializer
{
    public static async Task Initialize(IDbConnectionFactory factory)
    {
        using var connection = await factory.CreateConnectionAsync();

        if (connection is SqlConnection)
        {
            await InitializeSqlServer(connection);
        }
        else
        {
            await InitializeSqlite(connection);
        }
    }

    private static async Task InitializeSqlite(System.Data.Common.DbConnection connection)
    {
        try { await connection.ExecuteAsync("PRAGMA journal_mode=WAL;"); } catch { /* not supported on Turso */ }
        try { await connection.ExecuteAsync("PRAGMA foreign_keys=ON;"); } catch { /* not supported on Turso */ }

        var schema = GetEmbeddedSchema("MyMemo.Shared.Database.schema.sql");
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
            "ALTER TABLE sessions ADD COLUMN transcription_mode TEXT NOT NULL DEFAULT 'whisper'",
            "UPDATE infographics SET image_content = svg_content WHERE image_content IS NULL AND svg_content IS NOT NULL",
            "ALTER TABLE infographics DROP COLUMN svg_content",
            "ALTER TABLE memos ADD COLUMN updated_at TEXT",
        ];
        foreach (var sql in migrations)
        {
            try { await connection.ExecuteAsync(sql); }
            catch { /* Column already exists — ignore */ }
        }
    }

    private static async Task InitializeSqlServer(System.Data.Common.DbConnection connection)
    {
        var schema = GetEmbeddedSchema("MyMemo.Shared.Database.schema-sqlserver.sql");
        await connection.ExecuteAsync(schema);

        // Idempotent migrations using IF NOT EXISTS guards
        string[] migrations =
        [
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('transcriptions') AND name = 'transcription_duration_ms')
            ALTER TABLE transcriptions ADD transcription_duration_ms BIGINT
            """,
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('memos') AND name = 'generation_duration_ms')
            ALTER TABLE memos ADD generation_duration_ms BIGINT
            """,
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('sessions') AND name = 'memo_queued')
            ALTER TABLE sessions ADD memo_queued INT NOT NULL DEFAULT 0
            """,
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('sessions') AND name = 'context')
            ALTER TABLE sessions ADD context NVARCHAR(MAX)
            """,
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('infographics') AND name = 'image_content')
            ALTER TABLE infographics ADD image_content NVARCHAR(MAX)
            """,
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('sessions') AND name = 'transcription_mode')
            ALTER TABLE sessions ADD transcription_mode NVARCHAR(32) NOT NULL DEFAULT 'whisper'
            """,
            """
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('memos') AND name = 'updated_at')
            ALTER TABLE memos ADD updated_at NVARCHAR(30)
            """,
        ];
        foreach (var sql in migrations)
            await connection.ExecuteAsync(sql);
    }

    private static IEnumerable<string> SplitStatements(string sql) =>
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Where(s => s.Length > 0);

    private static string GetEmbeddedSchema(string resourceName)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"{resourceName} not found as embedded resource");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
