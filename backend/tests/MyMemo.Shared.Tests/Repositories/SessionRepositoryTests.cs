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

    [Fact]
    public async Task IsFinalizedAsync_ReturnsFalse_WhenNotFinalized()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        var result = await _sut.IsFinalizedAsync(session.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsFinalizedAsync_ReturnsTrue_WhenFinalized()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");
        await _sut.SetEndedAtAsync(session.Id);

        var result = await _sut.IsFinalizedAsync(session.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateOutputModeAsync_ChangesOutputMode()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.UpdateOutputModeAsync(session.Id, "product-planning");

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.OutputMode.Should().Be("product-planning");
    }

    [Fact]
    public async Task CreateAsync_StoresContext()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone", "Møde med København");

        session.Context.Should().Be("Møde med København");
    }

    [Fact]
    public async Task CreateAsync_ContextIsNullByDefault()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        session.Context.Should().BeNull();
    }

    [Fact]
    public async Task UpdateContextAsync_ChangesContext()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.UpdateContextAsync(session.Id, "Ny kontekst");

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.Context.Should().Be("Ny kontekst");
    }

    [Fact]
    public async Task UpdateContextAsync_CanClearContext()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone", "Original");

        await _sut.UpdateContextAsync(session.Id, null);

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.Context.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTitleAsync_SetsTitle()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.UpdateTitleAsync(session.Id, "Budgetmøde");

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.Title.Should().Be("Budgetmøde");
    }

    [Fact]
    public async Task UpdateTitleAsync_OverwritesExistingTitle()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.UpdateTitleAsync(session.Id, "Første titel");
        await _sut.UpdateTitleAsync(session.Id, "Ny titel");

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.Title.Should().Be("Ny titel");
    }

    public void Dispose() => _db.Dispose();
}
