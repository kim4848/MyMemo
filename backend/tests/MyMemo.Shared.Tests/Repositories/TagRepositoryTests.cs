using FluentAssertions;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class TagRepositoryTests : IDisposable
{
    private readonly TestDbFixture _db = new();
    private readonly TagRepository _sut;
    private readonly UserRepository _users;
    private readonly SessionRepository _sessions;

    public TagRepositoryTests()
    {
        _sut = new TagRepository(_db.Factory);
        _users = new UserRepository(_db.Factory);
        _sessions = new SessionRepository(_db.Factory);
    }

    private async Task<string> CreateTestUser()
    {
        var user = await _users.GetOrCreateByClerkIdAsync("clerk_tag_test", "tag@test.com", "Tag Test");
        return user.Id;
    }

    private async Task<string> CreateTestSession(string userId)
    {
        var session = await _sessions.CreateAsync(userId, "full", "microphone");
        return session.Id;
    }

    [Fact]
    public async Task CreateAsync_ReturnsNewTag()
    {
        var userId = await CreateTestUser();
        var tag = await _sut.CreateAsync(userId, "Important", "#ff0000");

        tag.Should().NotBeNull();
        tag.Name.Should().Be("Important");
        tag.Color.Should().Be("#ff0000");
        tag.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task ListByUserAsync_ReturnsUserTags()
    {
        var userId = await CreateTestUser();
        await _sut.CreateAsync(userId, "Tag A");
        await _sut.CreateAsync(userId, "Tag B");

        var tags = await _sut.ListByUserAsync(userId);

        tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTag()
    {
        var userId = await CreateTestUser();
        var tag = await _sut.CreateAsync(userId, "ToDelete");

        await _sut.DeleteAsync(tag.Id);

        var tags = await _sut.ListByUserAsync(userId);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameAndColor()
    {
        var userId = await CreateTestUser();
        var tag = await _sut.CreateAsync(userId, "Old Name", "#000000");

        await _sut.UpdateAsync(tag.Id, "New Name", "#ffffff");

        var tags = await _sut.ListByUserAsync(userId);
        tags.Should().ContainSingle(t => t.Name == "New Name" && t.Color == "#ffffff");
    }

    [Fact]
    public async Task AddTagToSessionAsync_LinksTagToSession()
    {
        var userId = await CreateTestUser();
        var sessionId = await CreateTestSession(userId);
        var tag = await _sut.CreateAsync(userId, "Meeting");

        await _sut.AddTagToSessionAsync(sessionId, tag.Id);

        var tags = await _sut.GetTagsForSessionAsync(sessionId);
        tags.Should().ContainSingle(t => t.Id == tag.Id);
    }

    [Fact]
    public async Task AddTagToSessionAsync_DuplicateIsIgnored()
    {
        var userId = await CreateTestUser();
        var sessionId = await CreateTestSession(userId);
        var tag = await _sut.CreateAsync(userId, "Duplicate");

        await _sut.AddTagToSessionAsync(sessionId, tag.Id);
        // Second call should not throw (try-catch handles duplicate)
        await _sut.AddTagToSessionAsync(sessionId, tag.Id);

        var tags = await _sut.GetTagsForSessionAsync(sessionId);
        tags.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveTagFromSessionAsync_UnlinksTag()
    {
        var userId = await CreateTestUser();
        var sessionId = await CreateTestSession(userId);
        var tag = await _sut.CreateAsync(userId, "Remove Me");

        await _sut.AddTagToSessionAsync(sessionId, tag.Id);
        await _sut.RemoveTagFromSessionAsync(sessionId, tag.Id);

        var tags = await _sut.GetTagsForSessionAsync(sessionId);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTagsForSessionsAsync_ReturnsBatchResults()
    {
        var userId = await CreateTestUser();
        var session1 = await CreateTestSession(userId);
        var session2 = await CreateTestSession(userId);
        var tag1 = await _sut.CreateAsync(userId, "Batch1");
        var tag2 = await _sut.CreateAsync(userId, "Batch2");

        await _sut.AddTagToSessionAsync(session1, tag1.Id);
        await _sut.AddTagToSessionAsync(session2, tag2.Id);

        var result = await _sut.GetTagsForSessionsAsync([session1, session2]);

        result.Should().ContainKey(session1);
        result.Should().ContainKey(session2);
        result[session1].Should().ContainSingle(t => t.Id == tag1.Id);
        result[session2].Should().ContainSingle(t => t.Id == tag2.Id);
    }

    [Fact]
    public async Task GetTagsForSessionsAsync_EmptyIds_ReturnsEmptyDictionary()
    {
        var result = await _sut.GetTagsForSessionsAsync([]);
        result.Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
