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

public class MemoEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IMemoTriggerService _memoTrigger;
    private readonly IDbConnectionFactory _dbFactory;

    public MemoEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _memoTrigger = Substitute.For<IMemoTriggerService>();

        IDbConnectionFactory? capturedDbFactory = null;

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
                capturedDbFactory = dbFactory;
                services.AddSingleton(Substitute.For<IBlobStorageService>());
                services.AddSingleton(Substitute.For<IQueueService>());
                services.AddScoped<IMemoTriggerService>(_ => _memoTrigger);
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
        _dbFactory = capturedDbFactory!;
    }

    private async Task UploadChunk(string sessionId, int chunkIndex)
    {
        using var conn = await _dbFactory.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "INSERT INTO chunks (id, session_id, chunk_index, blob_path, status, created_at, updated_at) VALUES (@id, @sessionId, @chunkIndex, @blobPath, 'uploaded', @now, @now)",
            new { id, sessionId, chunkIndex, blobPath = $"path/{chunkIndex}.webm", now });
    }

    [Fact]
    public async Task FinalizeSession_Returns202()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        await UploadChunk(session!.Id, 0);

        var response = await _client.PostAsync($"/api/sessions/{session.Id}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task FinalizeSession_Returns400_WhenNoChunks()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        var response = await _client.PostAsync($"/api/sessions/{session!.Id}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    [Fact]
    public async Task RegenerateMemo_Returns202_WhenCompleted()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        await UploadChunk(session!.Id, 0);
        await _client.PostAsync($"/api/sessions/{session.Id}/finalize", null);

        // Manually set status to completed
        using var conn = await _dbFactory.CreateConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE sessions SET status = 'completed' WHERE id = @id", new { id = session.Id });

        // Insert a memo so the session looks like it's done
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "INSERT INTO memos (id, session_id, output_mode, content, model_used, generation_duration_ms, created_at) VALUES (@id, @sessionId, 'full', 'test', 'gpt', 3200, @now)",
            new { id = Guid.NewGuid().ToString("N"), sessionId = session.Id, now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") });

        var response = await _client.PostAsJsonAsync($"/api/sessions/{session.Id}/regenerate", new { outputMode = "summary" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task RegenerateMemo_Returns400_WhenStillProcessing()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        await UploadChunk(session!.Id, 0);
        await _client.PostAsync($"/api/sessions/{session.Id}/finalize", null);

        var response = await _client.PostAsJsonAsync($"/api/sessions/{session.Id}/regenerate", new { outputMode = "summary" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegenerateMemo_Returns400_ForInvalidMode()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        using var conn = await _dbFactory.CreateConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE sessions SET status = 'completed' WHERE id = @id", new { id = session!.Id });

        var response = await _client.PostAsJsonAsync($"/api/sessions/{session.Id}/regenerate", new { outputMode = "invalid-mode" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record SessionIdResponse(string Id);
}
