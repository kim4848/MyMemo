namespace MyMemo.Shared.Models;

public sealed class User
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required string ClerkId { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
