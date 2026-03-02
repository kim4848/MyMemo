using FluentAssertions;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class UserRepositoryTests : IDisposable
{
    private readonly TestDbFixture _db = new();
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        _sut = new UserRepository(_db.Factory);
    }

    [Fact]
    public async Task GetOrCreateByClerkId_CreatesNewUser_WhenNotExists()
    {
        var user = await _sut.GetOrCreateByClerkIdAsync("clerk_123", "test@example.com", "Test User");

        user.Should().NotBeNull();
        user.ClerkId.Should().Be("clerk_123");
        user.Email.Should().Be("test@example.com");
        user.Name.Should().Be("Test User");
        user.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetOrCreateByClerkId_ReturnsExistingUser_WhenExists()
    {
        var first = await _sut.GetOrCreateByClerkIdAsync("clerk_123", "test@example.com", "Test User");
        var second = await _sut.GetOrCreateByClerkIdAsync("clerk_123", "test@example.com", "Test User");

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        var created = await _sut.GetOrCreateByClerkIdAsync("clerk_456", "other@example.com", "Other");
        var found = await _sut.GetByIdAsync(created.Id);

        found.Should().NotBeNull();
        found!.Email.Should().Be("other@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        var result = await _sut.GetByIdAsync("nonexistent");
        result.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
