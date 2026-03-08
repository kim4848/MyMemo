using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyMemo.Api.Tests.Auth;
using MyMemo.Shared.Database;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using NSubstitute;

namespace MyMemo.Api.Tests.Endpoints;

public class InfographicEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IInfographicService _infographicService;
    private readonly IDbConnectionFactory _dbFactory;

    public InfographicEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _infographicService = Substitute.For<IInfographicService>();

        IDbConnectionFactory? capturedDbFactory = null;

        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                var dbName = $"infographictest_{Guid.NewGuid():N}";
                var connString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
                var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connString);
                keepAlive.Open();
                services.AddSingleton(keepAlive);
                var dbFactory = new SqliteConnectionFactory(connString);
                DatabaseInitializer.Initialize(dbFactory).GetAwaiter().GetResult();
                services.AddSingleton<IDbConnectionFactory>(dbFactory);
                capturedDbFactory = dbFactory;
                services.AddSingleton(Substitute.For<IBlobStorageService>());
                services.AddSingleton(Substitute.For<IQueueService>());
                services.AddSingleton(_infographicService);
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
        _dbFactory = capturedDbFactory!;
    }

    private async Task<string> CreateSessionWithMemo()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        using var conn = await _dbFactory.CreateConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE sessions SET status = 'completed' WHERE id = @id", new { id = session!.Id });
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "INSERT INTO memos (id, session_id, output_mode, content, model_used, created_at) VALUES (@id, @sessionId, 'full', 'Test memo content', 'gpt-4.1-mini', @now)",
            new { id = Guid.NewGuid().ToString("N"), sessionId = session.Id, now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") });

        return session.Id;
    }

    [Fact]
    public async Task GenerateInfographic_Returns200_WithMemo()
    {
        var sessionId = await CreateSessionWithMemo();

        _infographicService.GenerateAsync("Test memo content", "full")
            .Returns(new InfographicResult("<svg>test</svg>", "gpt-4.1-mini", 200, 800));

        var response = await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var infographic = await response.Content.ReadFromJsonAsync<InfographicResponse>();
        infographic.Should().NotBeNull();
        infographic!.SvgContent.Should().Be("<svg>test</svg>");
    }

    [Fact]
    public async Task GenerateInfographic_Returns400_WithoutMemo()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        var response = await _client.PostAsync($"/api/sessions/{session!.Id}/infographic", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateInfographic_Returns404_ForUnknownSession()
    {
        var response = await _client.PostAsync("/api/sessions/nonexistent/infographic", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetInfographic_Returns404_WhenNotGenerated()
    {
        var sessionId = await CreateSessionWithMemo();

        var response = await _client.GetAsync($"/api/sessions/{sessionId}/infographic");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetInfographic_Returns200_AfterGeneration()
    {
        var sessionId = await CreateSessionWithMemo();

        _infographicService.GenerateAsync("Test memo content", "full")
            .Returns(new InfographicResult("<svg>infographic</svg>", "gpt-4.1-mini", 150, 600));

        await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);

        var response = await _client.GetAsync($"/api/sessions/{sessionId}/infographic");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var infographic = await response.Content.ReadFromJsonAsync<InfographicResponse>();
        infographic!.SvgContent.Should().Be("<svg>infographic</svg>");
    }

    [Fact]
    public async Task DeleteInfographic_Returns204()
    {
        var sessionId = await CreateSessionWithMemo();

        _infographicService.GenerateAsync("Test memo content", "full")
            .Returns(new InfographicResult("<svg>del</svg>", "gpt-4.1-mini", 100, 400));

        await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);

        var response = await _client.DeleteAsync($"/api/sessions/{sessionId}/infographic");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/sessions/{sessionId}/infographic");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateInfographic_ReplacesExisting()
    {
        var sessionId = await CreateSessionWithMemo();

        _infographicService.GenerateAsync("Test memo content", "full")
            .Returns(
                new InfographicResult("<svg>first</svg>", "gpt-4.1-mini", 100, 400),
                new InfographicResult("<svg>second</svg>", "gpt-4.1-mini", 100, 400));

        await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);
        var response = await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var infographic = await response.Content.ReadFromJsonAsync<InfographicResponse>();
        infographic!.SvgContent.Should().Be("<svg>second</svg>");
    }

    private sealed record SessionIdResponse(string Id);
    private sealed record InfographicResponse(string Id, string SessionId, string SvgContent, string ModelUsed);
}
