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

public class SessionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SessionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                var dbName = $"sessiontest_{Guid.NewGuid():N}";
                var connString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
                var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connString);
                keepAlive.Open();
                // Register in DI to prevent GC from closing the connection
                services.AddSingleton(keepAlive);
                var dbFactory = new SqliteConnectionFactory(connString);
                DatabaseInitializer.Initialize(dbFactory).GetAwaiter().GetResult();
                services.AddSingleton<IDbConnectionFactory>(dbFactory);
                services.AddSingleton(Substitute.For<IBlobStorageService>());
                services.AddSingleton(Substitute.For<IQueueService>());
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
    }

    [Fact]
    public async Task CreateSession_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
        body!.Status.Should().Be("recording");
        body.OutputMode.Should().Be("full");
    }

    [Fact]
    public async Task ListSessions_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSession_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_Returns204()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await createResponse.Content.ReadFromJsonAsync<SessionResponse>();

        var response = await _client.DeleteAsync($"/api/sessions/{session!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateSession_WithContext_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone", context = "Møde med København" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SessionWithContextResponse>();
        body!.Context.Should().Be("Møde med København");
    }

    [Fact]
    public async Task RenameSpeaker_Returns404_WhenSessionNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions/nonexistent/rename-speaker", new { oldName = "Speaker 0:", newName = "Kim:" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameSpeaker_ReturnsBadRequest_WhenNamesEmpty()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await createResponse.Content.ReadFromJsonAsync<SessionResponse>();

        var response = await _client.PostAsJsonAsync($"/api/sessions/{session!.Id}/rename-speaker", new { oldName = "", newName = "Kim" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RenameSpeaker_ReturnsOk_WhenSessionExists()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await createResponse.Content.ReadFromJsonAsync<SessionResponse>();

        var response = await _client.PostAsJsonAsync($"/api/sessions/{session!.Id}/rename-speaker", new { oldName = "Speaker 0:", newName = "Kim:" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RenameSpeakerResponse>();
        body!.Replaced.Should().BeTrue();
    }

    private sealed record SessionResponse(string Id, string Status, string OutputMode);
    private sealed record SessionWithContextResponse(string Id, string Status, string OutputMode, string? Context);
    private sealed record RenameSpeakerResponse(bool Replaced);
}
