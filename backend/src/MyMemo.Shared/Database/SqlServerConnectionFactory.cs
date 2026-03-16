using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace MyMemo.Shared.Database;

public sealed class SqlServerConnectionFactory(string connectionString) : IDbConnectionFactory
{
    private const int MaxRetries = 4;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

    // Azure SQL transient error numbers that warrant a retry
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        40613, // Database not currently available
        40197, // Service error processing request
        40501, // Service is busy
        49918, // Not enough resources to process request
        49919, // Cannot process create or update request
        49920, // Cannot process request due to too many operations
        4221,  // Login to read-secondary failed due to long wait on HADR
    ];

    public async Task<DbConnection> CreateConnectionAsync()
    {
        for (var attempt = 0; ; attempt++)
        {
            var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch (SqlException ex) when (attempt < MaxRetries && IsTransient(ex))
            {
                await connection.DisposeAsync();
                var delay = InitialDelay * Math.Pow(2, attempt);
                await Task.Delay(delay);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    private static bool IsTransient(SqlException ex)
    {
        foreach (SqlError error in ex.Errors)
        {
            if (TransientErrorNumbers.Contains(error.Number))
                return true;
        }
        return false;
    }
}
