using Microsoft.Data.Sqlite;
using MyMemo.Shared.Database;

namespace MyMemo.Shared.Tests.Repositories;

/// <summary>
/// Uses a shared-cache in-memory SQLite database so each repository
/// can open/close its own connection without destroying the data.
/// One "keeper" connection stays open for the lifetime of the test.
/// </summary>
public sealed class TestDbFixture : IDisposable
{
    private static int _counter;
    private readonly SqliteConnection _keepAlive;

    public IDbConnectionFactory Factory { get; }

    public TestDbFixture()
    {
        var dbName = $"testdb_{Interlocked.Increment(ref _counter)}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        Factory = new SqliteConnectionFactory(connectionString);
        DatabaseInitializer.Initialize(Factory).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
    }
}
