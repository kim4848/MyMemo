using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Database.Turso;

var tursoUrl = Environment.GetEnvironmentVariable("TURSO_URL")
    ?? throw new InvalidOperationException("TURSO_URL environment variable is required");
var tursoToken = Environment.GetEnvironmentVariable("TURSO_AUTH_TOKEN")
    ?? throw new InvalidOperationException("TURSO_AUTH_TOKEN environment variable is required");
var sqlConnStr = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
    ?? throw new InvalidOperationException("SQL_CONNECTION_STRING environment variable is required");

var source = new TursoConnectionFactory(tursoUrl, tursoToken);
var target = new SqlServerConnectionFactory(sqlConnStr);

// Initialize target schema
await DatabaseInitializer.Initialize(target);

// Tables in FK-safe order
string[] tables =
[
    "users",
    "tags",
    "sessions",
    "session_tags",
    "chunks",
    "transcriptions",
    "memos",
    "batch_transcription_jobs",
    "infographics",
];

foreach (var table in tables)
{
    Console.Write($"Migrating {table}...");

    using var srcConn = await source.CreateConnectionAsync();
    var rows = (await srcConn.QueryAsync($"SELECT * FROM {table}")).ToList();

    if (rows.Count == 0)
    {
        Console.WriteLine(" 0 rows");
        continue;
    }

    using var tgtConn = await target.CreateConnectionAsync();

    // Build INSERT from the columns in the first row
    var columns = ((IDictionary<string, object?>)rows[0]).Keys.ToList();
    var colList = string.Join(", ", columns);
    var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
    var insertSql = $"SET IDENTITY_INSERT [{table}] OFF; INSERT INTO [{table}] ({colList}) VALUES ({paramList})";

    // Try without IDENTITY_INSERT first (tables don't use identity columns)
    insertSql = $"INSERT INTO [{table}] ({colList}) VALUES ({paramList})";

    var count = 0;
    foreach (var row in rows)
    {
        try
        {
            await tgtConn.ExecuteAsync(insertSql, row);
            count++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n  Error inserting into {table}: {ex.Message}");
        }
    }

    Console.WriteLine($" {count}/{rows.Count} rows");
}

Console.WriteLine("Migration complete.");
