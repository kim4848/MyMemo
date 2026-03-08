namespace MyMemo.Shared.Services;

public sealed record InfographicResult(string ImageBase64, string ImageFormat, string ModelUsed);

public interface IInfographicService
{
    Task<InfographicResult> GenerateAsync(string memoContent, string outputMode);
}
