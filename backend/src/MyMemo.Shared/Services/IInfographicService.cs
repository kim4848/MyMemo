namespace MyMemo.Shared.Services;

public sealed record InfographicResult(string SvgContent, string ModelUsed, int PromptTokens, int CompletionTokens);

public interface IInfographicService
{
    Task<InfographicResult> GenerateAsync(string memoContent, string outputMode);
}
