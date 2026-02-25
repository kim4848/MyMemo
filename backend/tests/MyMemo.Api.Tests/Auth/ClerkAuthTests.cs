using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyMemo.Shared.Database;
using MyMemo.Shared.Services;
using NSubstitute;

namespace MyMemo.Api.Tests.Auth;

public class ClerkAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ClerkAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                // Replace real services with test doubles
                var dbName = $"authtest_{Guid.NewGuid():N}";
                var connString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
                var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connString);
                keepAlive.Open();
                var dbFactory = new SqliteConnectionFactory(connString);
                DatabaseInitializer.Initialize(dbFactory).GetAwaiter().GetResult();
                services.AddSingleton<IDbConnectionFactory>(dbFactory);
                services.AddSingleton(Substitute.For<IBlobStorageService>());
                services.AddSingleton(Substitute.For<IQueueService>());
            });
        });
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
