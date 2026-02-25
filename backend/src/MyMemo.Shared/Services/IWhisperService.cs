namespace MyMemo.Shared.Services;

public sealed record WhisperResult(string Text, double? AverageConfidence, string? WordTimestampsJson);

public interface IWhisperService
{
    Task<WhisperResult> TranscribeAsync(Stream audioStream, string language = "da");
}
