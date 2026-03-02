using Microsoft.Data.Sqlite;

namespace MyMemo.Shared.Database;

public interface IDbConnectionFactory
{
    Task<SqliteConnection> CreateConnectionAsync();
}
