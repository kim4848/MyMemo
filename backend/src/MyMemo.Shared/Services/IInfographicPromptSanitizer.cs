namespace MyMemo.Shared.Services;

public interface IInfographicPromptSanitizer
{
    Task<string> SanitizeAsync(string memoContent, CancellationToken ct = default);
}
