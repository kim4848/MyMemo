using FluentAssertions;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class MemoRepositoryTests : IDisposable
{
    private readonly TestDbFixture _db = new();
    private readonly MemoRepository _sut;
    private readonly SessionRepository _sessions;
    private readonly UserRepository _users;

    public MemoRepositoryTests()
    {
        _sut = new MemoRepository(_db.Factory);
        _sessions = new SessionRepository(_db.Factory);
        _users = new UserRepository(_db.Factory);
    }

    private async Task<string> CreateTestSession()
    {
        var user = await _users.GetOrCreateByClerkIdAsync("clerk_test", "test@test.com", "Test");
        var session = await _sessions.CreateAsync(user.Id, "full", "microphone");
        return session.Id;
    }

    [Fact]
    public async Task DeleteBySessionIdAsync_RemovesMemo()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, "full", "Test content", "gpt-4.1-mini", 100, 50, 3200);

        var before = await _sut.GetBySessionIdAsync(sessionId);
        before.Should().NotBeNull();

        await _sut.DeleteBySessionIdAsync(sessionId);

        var after = await _sut.GetBySessionIdAsync(sessionId);
        after.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_StoresGenerationDuration()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, "full", "Test content", "gpt-4.1-mini", 100, 50, 3200);

        var memo = await _sut.GetBySessionIdAsync(sessionId);
        memo.Should().NotBeNull();
        memo!.GenerationDurationMs.Should().Be(3200);
    }

    [Fact]
    public async Task DeleteBySessionIdAsync_NoOpWhenNoMemo()
    {
        var sessionId = await CreateTestSession();

        // Should not throw
        await _sut.DeleteBySessionIdAsync(sessionId);

        var result = await _sut.GetBySessionIdAsync(sessionId);
        result.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
