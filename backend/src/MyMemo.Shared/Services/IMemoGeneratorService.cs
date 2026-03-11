namespace MyMemo.Shared.Services;

public sealed record MemoResult(string Content, string ModelUsed, int PromptTokens, int CompletionTokens);

public interface IMemoGeneratorService
{
    Task<MemoResult> GenerateAsync(string fullTranscription, string outputMode, string? context = null);
    Task<string> GenerateTitleAsync(string fullTranscription);
}
