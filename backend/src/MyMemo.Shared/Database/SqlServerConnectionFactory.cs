using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace MyMemo.Shared.Database;

public sealed class SqlServerConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<DbConnection> CreateConnectionAsync()
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
