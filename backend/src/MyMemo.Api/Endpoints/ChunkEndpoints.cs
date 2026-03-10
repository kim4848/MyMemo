using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Api.Endpoints;

public static class ChunkEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions/{sessionId}/chunks", UploadChunk)
            .RequireAuthorization()
            .DisableAntiforgery();
    }

    private static async Task<IResult> UploadChunk(
        string sessionId,
        IFormFile audio,
        [FromForm] int chunkIndex,
        ISessionRepository sessions,
        IChunkRepository chunks,
        IUserRepository users,
        IBlobStorageService blobService,
        IQueueService queueService,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var blobPath = $"{user.Id}/{sessionId}/{chunkIndex}.webm";
        await using var stream = audio.OpenReadStream();
        await blobService.UploadAsync(blobPath, stream, audio.ContentType);

        var chunk = await chunks.CreateAsync(sessionId, chunkIndex, blobPath);
        await chunks.UpdateStatusAsync(chunk.Id, "queued");
        await queueService.SendTranscriptionJobAsync(sessionId, chunk.Id, chunkIndex, blobPath, transcriptionMode: session.TranscriptionMode);

        return Results.Accepted($"/api/sessions/{sessionId}", chunk);
    }
}
