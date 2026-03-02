namespace MyMemo.Shared.Models;

public sealed class Chunk
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public required int ChunkIndex { get; init; }
    public required string BlobPath { get; init; }
    public int? DurationSec { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
