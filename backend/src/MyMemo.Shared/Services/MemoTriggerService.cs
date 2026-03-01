using Microsoft.Extensions.Logging;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Services;

public interface IMemoTriggerService
{
    Task<bool> TryQueueMemoGenerationAsync(string sessionId);
}

public sealed class MemoTriggerService(
    ISessionRepository sessions,
    IChunkRepository chunks,
    IQueueService queueService,
    ILogger<MemoTriggerService> logger) : IMemoTriggerService
{
    public async Task<bool> TryQueueMemoGenerationAsync(string sessionId)
    {
        if (!await sessions.IsFinalizedAsync(sessionId))
        {
            logger.LogDebug("Session {SessionId} not finalized, skipping memo trigger", sessionId);
            return false;
        }

        var (chunkCount, allTranscribed) = await chunks.GetTranscriptionStatusAsync(sessionId);
        if (chunkCount == 0)
        {
            logger.LogWarning("Session {SessionId} has zero chunks, skipping memo trigger", sessionId);
            return false;
        }

        if (!allTranscribed)
        {
            logger.LogDebug("Session {SessionId} has untranscribed chunks, skipping memo trigger", sessionId);
            return false;
        }

        // Atomic check-and-set: only the first caller queues the message
        if (!await sessions.TrySetMemoQueuedAsync(sessionId))
        {
            logger.LogDebug("Session {SessionId} memo already queued, skipping", sessionId);
            return false;
        }

        await queueService.SendMemoGenerationJobAsync(sessionId);
        logger.LogInformation("Memo generation queued for session {SessionId}", sessionId);
        return true;
    }
}
