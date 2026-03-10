namespace MyMemo.Shared.Services;

public enum BatchTranscriptionStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed,
}

public sealed record SpeakerSegment(int SpeakerId, string Text, double StartSeconds, double EndSeconds);

public sealed record BatchTranscriptionResult(IReadOnlyList<SpeakerSegment> Segments, string ReadableText);

public interface ISpeechBatchTranscriptionService
{
    Task<string> SubmitAsync(string sasUrl, string language = "da-DK");
    Task<BatchTranscriptionStatus> GetStatusAsync(string jobId);
    Task<BatchTranscriptionResult> GetResultAsync(string jobId);
    Task DeleteAsync(string jobId);
}
