using FluentAssertions;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class ChunkRepositoryTests : IDisposable
{
    private readonly TestDbFixture _db = new();
    private readonly ChunkRepository _sut;
    private readonly UserRepository _users;
    private readonly SessionRepository _sessions;

    public ChunkRepositoryTests()
    {
        _sut = new ChunkRepository(_db.Factory);
        _users = new UserRepository(_db.Factory);
        _sessions = new SessionRepository(_db.Factory);
    }

    private async Task<string> CreateTestSession()
    {
        var user = await _users.GetOrCreateByClerkIdAsync("clerk_test", "t@t.com", "T");
        var session = await _sessions.CreateAsync(user.Id, "full", "microphone");
        return session.Id;
    }

    [Fact]
    public async Task CreateAsync_ReturnsNewChunk()
    {
        var sessionId = await CreateTestSession();
        var chunk = await _sut.CreateAsync(sessionId, 0, "user/session/0.webm");

        chunk.Should().NotBeNull();
        chunk.SessionId.Should().Be(sessionId);
        chunk.ChunkIndex.Should().Be(0);
        chunk.Status.Should().Be("uploaded");
    }

    [Fact]
    public async Task ListBySessionAsync_ReturnsChunksInOrder()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        var chunks = await _sut.ListBySessionAsync(sessionId);

        chunks.Should().HaveCount(2);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[1].ChunkIndex.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var sessionId = await CreateTestSession();
        var chunk = await _sut.CreateAsync(sessionId, 0, "path/0.webm");

        await _sut.UpdateStatusAsync(chunk.Id, "transcribed");

        var updated = await _sut.GetByIdAsync(chunk.Id);
        updated!.Status.Should().Be("transcribed");
    }

    [Fact]
    public async Task AreAllTranscribedAsync_ReturnsTrueWhenAllDone()
    {
        var sessionId = await CreateTestSession();
        var c1 = await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        var c2 = await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        await _sut.UpdateStatusAsync(c1.Id, "transcribed");
        await _sut.UpdateStatusAsync(c2.Id, "transcribed");

        var result = await _sut.AreAllTranscribedAsync(sessionId);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreAllTranscribedAsync_ReturnsFalseWhenNotDone()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        var c2 = await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        await _sut.UpdateStatusAsync(c2.Id, "transcribed");

        var result = await _sut.AreAllTranscribedAsync(sessionId);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CountBySessionAsync_ReturnsZero_WhenNoChunks()
    {
        var sessionId = await CreateTestSession();

        var count = await _sut.CountBySessionAsync(sessionId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountBySessionAsync_ReturnsCorrectCount()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        var count = await _sut.CountBySessionAsync(sessionId);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_ReturnsCombinedStatus()
    {
        var sessionId = await CreateTestSession();
        var c1 = await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        await _sut.UpdateStatusAsync(c1.Id, "transcribed");

        var (count, allTranscribed) = await _sut.GetTranscriptionStatusAsync(sessionId);
        count.Should().Be(2);
        allTranscribed.Should().BeFalse();
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_AllTranscribed()
    {
        var sessionId = await CreateTestSession();
        var c1 = await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        var c2 = await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        await _sut.UpdateStatusAsync(c1.Id, "transcribed");
        await _sut.UpdateStatusAsync(c2.Id, "transcribed");

        var (count, allTranscribed) = await _sut.GetTranscriptionStatusAsync(sessionId);
        count.Should().Be(2);
        allTranscribed.Should().BeTrue();
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_ZeroChunks()
    {
        var sessionId = await CreateTestSession();

        var (count, allTranscribed) = await _sut.GetTranscriptionStatusAsync(sessionId);
        count.Should().Be(0);
        allTranscribed.Should().BeTrue();
    }

    public void Dispose() => _db.Dispose();
}
