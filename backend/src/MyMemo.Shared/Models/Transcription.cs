namespace MyMemo.Shared.Models;

public sealed class Transcription
{
    public required string Id { get; init; }
    public required string ChunkId { get; init; }
    public required string RawText { get; init; }
    public string Language { get; init; } = "da";
    public double? Confidence { get; init; }
    public string? WordTimestamps { get; init; }
    public long? TranscriptionDurationMs { get; init; }
    public required string CreatedAt { get; init; }
}
