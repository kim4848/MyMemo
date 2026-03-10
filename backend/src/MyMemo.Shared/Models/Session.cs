namespace MyMemo.Shared.Models;

public sealed class Session
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public string? Title { get; init; }
    public required string Status { get; init; }
    public required string OutputMode { get; init; }
    public required string AudioSource { get; init; }
    public string? Context { get; init; }
    public string TranscriptionMode { get; init; } = "whisper";
    public bool MemoQueued { get; init; }
    public required string StartedAt { get; init; }
    public string? EndedAt { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
