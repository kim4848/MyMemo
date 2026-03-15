using FluentAssertions;
using Microsoft.Data.SqlClient;
using MyMemo.Shared.Database;

namespace MyMemo.Shared.Tests.Database;

public class SqlServerConnectionFactoryTests
{
    [Fact]
    public void Constructor_AcceptsConnectionString()
    {
        var factory = new SqlServerConnectionFactory("Server=localhost;Database=test;");
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateConnectionAsync_ReturnsSqlConnection()
    {
        // Use a known-bad server so it fails fast, but verify the connection type
        var factory = new SqlServerConnectionFactory("Server=localhost,9999;Database=test;Connection Timeout=1;Encrypt=false");

        Func<Task> act = () => factory.CreateConnectionAsync();

        // Should throw because no SQL Server is running, but it proves the factory
        // creates a SqlConnection (not SqliteConnection)
        await act.Should().ThrowAsync<SqlException>();
    }

    [Fact]
    public void Factory_Implements_IDbConnectionFactory()
    {
        IDbConnectionFactory factory = new SqlServerConnectionFactory("Server=localhost;Database=test;");
        factory.Should().BeOfType<SqlServerConnectionFactory>();
    }
}
