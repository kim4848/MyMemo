namespace MyMemo.Shared.Services;

public sealed record InfographicResult(string ImageBase64, string ModelUsed, int? PromptTokens, int? CompletionTokens);

public interface IInfographicService
{
    Task<InfographicResult> GenerateAsync(string sessionId, string memoContent, string outputMode);
}
