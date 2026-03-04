using System.Data.Common;

namespace MyMemo.Shared.Database;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync();
}
