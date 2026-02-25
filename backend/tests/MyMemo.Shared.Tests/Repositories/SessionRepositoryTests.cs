using FluentAssertions;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class SessionRepositoryTests : IDisposable
{
    private readonly TestDbFixture _db = new();
    private readonly SessionRepository _sut;
    private readonly UserRepository _users;

    public SessionRepositoryTests()
    {
        _sut = new SessionRepository(_db.Factory);
        _users = new UserRepository(_db.Factory);
    }

    private async Task<string> CreateTestUser()
    {
        var user = await _users.GetOrCreateByClerkIdAsync("clerk_test", "test@test.com", "Test");
        return user.Id;
    }

    [Fact]
    public async Task CreateAsync_ReturnsNewSession()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        session.Should().NotBeNull();
        session.UserId.Should().Be(userId);
        session.Status.Should().Be("recording");
        session.OutputMode.Should().Be("full");
    }

    [Fact]
    public async Task ListByUserAsync_ReturnsOnlyUserSessions()
    {
        var userId = await CreateTestUser();
        await _sut.CreateAsync(userId, "full", "microphone");
        await _sut.CreateAsync(userId, "summary", "microphone");

        var sessions = await _sut.ListByUserAsync(userId);

        sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesSession()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.DeleteAsync(session.Id);

        var result = await _sut.GetByIdAsync(session.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.UpdateStatusAsync(session.Id, "processing");

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.Status.Should().Be("processing");
    }

    public void Dispose() => _db.Dispose();
}
