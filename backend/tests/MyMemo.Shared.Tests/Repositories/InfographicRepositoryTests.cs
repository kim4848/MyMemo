using FluentAssertions;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class InfographicRepositoryTests : IDisposable
{
    private readonly TestDbFixture _db = new();
    private readonly InfographicRepository _sut;
    private readonly SessionRepository _sessions;
    private readonly UserRepository _users;

    public InfographicRepositoryTests()
    {
        _sut = new InfographicRepository(_db.Factory);
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
    public async Task CreateAsync_StoresInfographic()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, "<svg></svg>", "gpt-4.1-mini", 200, 800, 5000);

        var infographic = await _sut.GetBySessionIdAsync(sessionId);
        infographic.Should().NotBeNull();
        infographic!.SessionId.Should().Be(sessionId);
        infographic.SvgContent.Should().Be("<svg></svg>");
        infographic.ModelUsed.Should().Be("gpt-4.1-mini");
        infographic.PromptTokens.Should().Be(200);
        infographic.CompletionTokens.Should().Be(800);
        infographic.GenerationDurationMs.Should().Be(5000);
    }

    [Fact]
    public async Task GetBySessionIdAsync_ReturnsNull_WhenNoInfographic()
    {
        var sessionId = await CreateTestSession();

        var result = await _sut.GetBySessionIdAsync(sessionId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBySessionIdAsync_RemovesInfographic()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, "<svg></svg>", "gpt-4.1-mini", 200, 800, 5000);

        var before = await _sut.GetBySessionIdAsync(sessionId);
        before.Should().NotBeNull();

        await _sut.DeleteBySessionIdAsync(sessionId);

        var after = await _sut.GetBySessionIdAsync(sessionId);
        after.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBySessionIdAsync_NoOpWhenNoInfographic()
    {
        var sessionId = await CreateTestSession();

        // Should not throw
        await _sut.DeleteBySessionIdAsync(sessionId);

        var result = await _sut.GetBySessionIdAsync(sessionId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_StoresGenerationDuration()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, "<svg>test</svg>", "gpt-4.1-mini", 100, 50, 7500);

        var infographic = await _sut.GetBySessionIdAsync(sessionId);
        infographic.Should().NotBeNull();
        infographic!.GenerationDurationMs.Should().Be(7500);
    }

    public void Dispose() => _db.Dispose();
}
