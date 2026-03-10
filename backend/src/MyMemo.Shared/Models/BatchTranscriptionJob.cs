namespace MyMemo.Shared.Models;

public sealed class BatchTranscriptionJob
{
    public required string Id { get; init; }
    public required string ChunkId { get; init; }
    public required string SessionId { get; init; }
    public required string AzureJobId { get; init; }
    public required string Status { get; init; }
    public required string CreatedAt { get; init; }
    public string? CompletedAt { get; init; }
}
