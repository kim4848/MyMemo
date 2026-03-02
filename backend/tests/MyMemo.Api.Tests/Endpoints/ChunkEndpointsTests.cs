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

public class ChunkEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IBlobStorageService _blobService;
    private readonly IQueueService _queueService;

    public ChunkEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _blobService = Substitute.For<IBlobStorageService>();
        _queueService = Substitute.For<IQueueService>();

        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                var dbName = $"chunktest_{Guid.NewGuid():N}";
                var connString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
                var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connString);
                keepAlive.Open();
                services.AddSingleton(keepAlive);
                var dbFactory = new SqliteConnectionFactory(connString);
                DatabaseInitializer.Initialize(dbFactory).GetAwaiter().GetResult();
                services.AddSingleton<IDbConnectionFactory>(dbFactory);
                services.AddSingleton(_blobService);
                services.AddSingleton(_queueService);
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
    }

    [Fact]
    public async Task UploadChunk_Returns202()
    {
        // Create a session first
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        _blobService.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>())
            .Returns("path/0.webm");

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "audio", "chunk.webm");
        content.Add(new StringContent("0"), "chunkIndex");

        var response = await _client.PostAsync($"/api/sessions/{session!.Id}/chunks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private sealed record SessionIdResponse(string Id);
}
