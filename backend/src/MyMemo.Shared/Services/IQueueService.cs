namespace MyMemo.Shared.Services;

public interface IQueueService
{
    Task SendTranscriptionJobAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language = "da", string transcriptionMode = "whisper");
    Task SendMemoGenerationJobAsync(string sessionId);
    Task SendInfographicGenerationJobAsync(string sessionId);
}
