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
