namespace MyMemo.Shared.Models;

public sealed class Memo
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public required string OutputMode { get; init; }
    public required string Content { get; init; }
    public required string ModelUsed { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public long? GenerationDurationMs { get; init; }
    public required string CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}
