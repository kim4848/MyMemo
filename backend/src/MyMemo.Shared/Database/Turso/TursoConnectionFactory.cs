using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoConnectionFactory(string url, string authToken) : IDbConnectionFactory
{
    private const int MaxRetries = 4;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

    public async Task<DbConnection> CreateConnectionAsync()
    {
        for (var attempt = 0; ; attempt++)
        {
            var conn = new TursoDbConnection(url, authToken);
            try
            {
                await conn.OpenAsync();
                return conn;
            }
            catch (Exception) when (attempt < MaxRetries)
            {
                await conn.DisposeAsync();
                var delay = InitialDelay * Math.Pow(2, attempt);
                await Task.Delay(delay);
            }
            catch
            {
                await conn.DisposeAsync();
                throw;
            }
        }
    }
}
