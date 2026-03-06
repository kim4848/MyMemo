using System.Security.Claims;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Api.Endpoints;

public static class MemoEndpoints
{
    private static readonly HashSet<string> ValidOutputModes = ["full", "summary", "product-planning"];

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions/{sessionId}/finalize", FinalizeSession)
            .RequireAuthorization();
        app.MapGet("/api/sessions/{sessionId}/memo", GetMemo)
            .RequireAuthorization();
        app.MapPost("/api/sessions/{sessionId}/regenerate", RegenerateMemo)
            .RequireAuthorization();
    }

    private static async Task<IResult> FinalizeSession(
        string sessionId,
        ISessionRepository sessions,
        IUserRepository users,
        IChunkRepository chunks,
        IQueueService queueService,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        if (await chunks.CountBySessionAsync(sessionId) == 0)
            return Results.BadRequest(new { error = "Cannot finalize a session with no chunks" });

        await sessions.SetEndedAtAsync(sessionId);
        await sessions.UpdateStatusAsync(sessionId, "processing");

        // Always queue memo generation — the worker checks AreAllTranscribedAsync
        // and skips if chunks aren't ready yet. The TranscriptionWorker also
        // triggers memo generation after each chunk, covering the case where
        // transcription finishes after finalize.
        await queueService.SendMemoGenerationJobAsync(sessionId);

        return Results.Accepted($"/api/sessions/{sessionId}/memo");
    }

    private static async Task<IResult> GetMemo(
        string sessionId,
        ISessionRepository sessions,
        IMemoRepository memos,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var memo = await memos.GetBySessionIdAsync(sessionId);
        if (memo is null) return Results.NotFound();

        return Results.Ok(memo);
    }

    private static async Task<IResult> RegenerateMemo(
        string sessionId,
        RegenerateRequest request,
        ISessionRepository sessions,
        IMemoRepository memos,
        IUserRepository users,
        IQueueService queueService,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        if (session.Status is not ("completed" or "failed"))
            return Results.BadRequest(new { error = "Can only regenerate completed or failed sessions" });

        if (!ValidOutputModes.Contains(request.OutputMode))
            return Results.BadRequest(new { error = $"Invalid output mode. Must be one of: {string.Join(", ", ValidOutputModes)}" });

        await memos.DeleteBySessionIdAsync(sessionId);
        await sessions.ResetMemoQueuedAsync(sessionId);
        await sessions.UpdateOutputModeAsync(sessionId, request.OutputMode);
        if (request.Context is not null)
            await sessions.UpdateContextAsync(sessionId, request.Context);
        await sessions.UpdateStatusAsync(sessionId, "processing");
        await queueService.SendMemoGenerationJobAsync(sessionId);

        return Results.Accepted($"/api/sessions/{sessionId}/memo");
    }

    private sealed record RegenerateRequest(string OutputMode, string? Context = null);
}
