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
    private readonly IQueueService _queueService;
    private readonly IDbConnectionFactory _dbFactory;

    public InfographicEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _queueService = Substitute.For<IQueueService>();

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
                services.AddSingleton(_queueService);
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
    public async Task GenerateInfographic_Returns202_WithMemo()
    {
        var sessionId = await CreateSessionWithMemo();

        var response = await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await _queueService.Received(1).SendInfographicGenerationJobAsync(sessionId);
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
    public async Task GetInfographic_Returns200_WhenExists()
    {
        var sessionId = await CreateSessionWithMemo();

        // Simulate worker having created the infographic
        using var conn = await _dbFactory.CreateConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "INSERT INTO infographics (id, session_id, image_content, image_format, model_used, created_at) VALUES (@id, @sessionId, 'dGVzdA==', 'png', 'gpt-image-1', @now)",
            new { id = Guid.NewGuid().ToString("N"), sessionId, now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") });

        var response = await _client.GetAsync($"/api/sessions/{sessionId}/infographic");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var infographic = await response.Content.ReadFromJsonAsync<InfographicResponse>();
        infographic!.ImageContent.Should().Be("dGVzdA==");
    }

    [Fact]
    public async Task DeleteInfographic_Returns204()
    {
        var sessionId = await CreateSessionWithMemo();

        // Simulate worker having created the infographic
        using var conn = await _dbFactory.CreateConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "INSERT INTO infographics (id, session_id, image_content, image_format, model_used, created_at) VALUES (@id, @sessionId, 'dGVzdA==', 'png', 'gpt-image-1', @now)",
            new { id = Guid.NewGuid().ToString("N"), sessionId, now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") });

        var response = await _client.DeleteAsync($"/api/sessions/{sessionId}/infographic");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/sessions/{sessionId}/infographic");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateInfographic_DeletesExistingBeforeQueuing()
    {
        var sessionId = await CreateSessionWithMemo();

        // Simulate existing infographic
        using var conn = await _dbFactory.CreateConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "INSERT INTO infographics (id, session_id, image_content, image_format, model_used, created_at) VALUES (@id, @sessionId, 'dGVzdA==', 'png', 'gpt-image-1', @now)",
            new { id = Guid.NewGuid().ToString("N"), sessionId, now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") });

        var response = await _client.PostAsync($"/api/sessions/{sessionId}/infographic", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Existing infographic should have been deleted
        var getResponse = await _client.GetAsync($"/api/sessions/{sessionId}/infographic");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _queueService.Received(1).SendInfographicGenerationJobAsync(sessionId);
    }

    private sealed record SessionIdResponse(string Id);
    private sealed record InfographicResponse(string Id, string SessionId, string ImageContent, string ImageFormat, string ModelUsed);
}
