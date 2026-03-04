using FluentAssertions;
using MyMemo.Shared.Database;
using MyMemo.Shared.Database.Turso;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Database;

public class TursoIntegrationTests
{
    private readonly IDbConnectionFactory? _factory;
    private readonly bool _skip;

    public TursoIntegrationTests()
    {
        var url = Environment.GetEnvironmentVariable("TURSO_URL");
        var token = Environment.GetEnvironmentVariable("TURSO_AUTH_TOKEN");
        _skip = string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token);
        if (!_skip)
        {
            _factory = new TursoConnectionFactory(url!, token!);
            DatabaseInitializer.Initialize(_factory).GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task UserRepository_CreateAndRetrieve()
    {
        if (_skip) return;

        var repo = new UserRepository(_factory!);
        var clerkId = $"test_{Guid.NewGuid():N}";
        var user = await repo.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test User");

        user.Should().NotBeNull();
        user.ClerkId.Should().Be(clerkId);

        var same = await repo.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test User");
        same.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task SessionRepository_CreateListDelete()
    {
        if (_skip) return;

        var users = new UserRepository(_factory!);
        var sessions = new SessionRepository(_factory!);

        var clerkId = $"test_{Guid.NewGuid():N}";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test");

        var session = await sessions.CreateAsync(user.Id, "full", "microphone");
        session.Should().NotBeNull();
        session.Status.Should().Be("recording");

        var found = await sessions.GetByIdAsync(session.Id);
        found.Should().NotBeNull();
        found!.UserId.Should().Be(user.Id);

        var list = await sessions.ListByUserAsync(user.Id);
        list.Should().Contain(s => s.Id == session.Id);

        await sessions.DeleteAsync(session.Id);
        var gone = await sessions.GetByIdAsync(session.Id);
        gone.Should().BeNull();
    }

    [Fact]
    public async Task ChunkRepository_CreateAndCount()
    {
        if (_skip) return;

        var users = new UserRepository(_factory!);
        var sessions = new SessionRepository(_factory!);
        var chunks = new ChunkRepository(_factory!);

        var clerkId = $"test_{Guid.NewGuid():N}";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test");
        var session = await sessions.CreateAsync(user.Id, "full", "microphone");

        var chunk = await chunks.CreateAsync(session.Id, 0, $"blobs/{session.Id}/0.webm");
        chunk.Should().NotBeNull();

        var count = await chunks.CountBySessionAsync(session.Id);
        count.Should().Be(1);

        // cleanup
        await sessions.DeleteAsync(session.Id);
    }
}
