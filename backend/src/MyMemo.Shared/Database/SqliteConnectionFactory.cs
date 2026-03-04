using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace MyMemo.Shared.Database;

public sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<DbConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
