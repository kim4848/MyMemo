namespace MyMemo.Shared.Models;

public sealed class Tag
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
    public required string CreatedAt { get; init; }
}
