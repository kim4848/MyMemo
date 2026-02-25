using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyMemo.Api.Tests.Auth;
using MyMemo.Shared.Database;
using MyMemo.Shared.Services;
using NSubstitute;

namespace MyMemo.Api.Tests.Endpoints;

public class MemoEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IQueueService _queueService;

    public MemoEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _queueService = Substitute.For<IQueueService>();

        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                var dbName = $"memotest_{Guid.NewGuid():N}";
                var connString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
                var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connString);
                keepAlive.Open();
                services.AddSingleton(keepAlive);
                var dbFactory = new SqliteConnectionFactory(connString);
                DatabaseInitializer.Initialize(dbFactory).GetAwaiter().GetResult();
                services.AddSingleton<IDbConnectionFactory>(dbFactory);
                services.AddSingleton(Substitute.For<IBlobStorageService>());
                services.AddSingleton(_queueService);
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
    }

    [Fact]
    public async Task FinalizeSession_Returns202()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        var response = await _client.PostAsync($"/api/sessions/{session!.Id}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetMemo_Returns404_WhenNoMemo()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        var response = await _client.GetAsync($"/api/sessions/{session!.Id}/memo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record SessionIdResponse(string Id);
}
