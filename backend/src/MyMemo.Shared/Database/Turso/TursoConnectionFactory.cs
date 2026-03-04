using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoConnectionFactory(string url, string authToken) : IDbConnectionFactory
{
    public Task<DbConnection> CreateConnectionAsync()
    {
        var conn = new TursoDbConnection(url, authToken);
        conn.Open();
        return Task.FromResult<DbConnection>(conn);
    }
}
