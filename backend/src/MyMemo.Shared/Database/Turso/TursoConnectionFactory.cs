using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoConnectionFactory(string url, string authToken) : IDbConnectionFactory
{
    public async Task<DbConnection> CreateConnectionAsync()
    {
        var conn = new TursoDbConnection(url, authToken);
        await conn.OpenAsync();
        return conn;
    }
}
