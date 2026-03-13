using System.Security.Claims;
using MyMemo.Shared.Repositories;

namespace MyMemo.Api.Endpoints;

public static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        group.MapPost("", CreateSession);
        group.MapGet("", ListSessions);
        group.MapGet("{id}", GetSession);
        group.MapDelete("{id}", DeleteSession);
        group.MapPost("{id}/rename-speaker", RenameSpeaker);
    }

    private static async Task<IResult> CreateSession(
        CreateSessionRequest request,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? "";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, email, name);

        var session = await sessions.CreateAsync(user.Id, request.OutputMode, request.AudioSource, request.Context, request.TranscriptionMode);
        return Results.Created($"/api/sessions/{session.Id}", session);
    }

    private static async Task<IResult> ListSessions(
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var list = await sessions.ListByUserAsync(user.Id);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetSession(
        string id,
        ISessionRepository sessions,
        IChunkRepository chunks,
        ITranscriptionRepository transcriptions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var session = await sessions.GetByIdAsync(id);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var sessionChunks = await chunks.ListBySessionAsync(id);
        var sessionTranscriptions = await transcriptions.ListBySessionAsync(id);
        var transcriptionDurations = sessionTranscriptions
            .Where(t => t.TranscriptionDurationMs.HasValue)
            .ToDictionary(t => t.ChunkId, t => t.TranscriptionDurationMs!.Value);

        var transcriptionTexts = sessionTranscriptions
            .Select(t => new { chunkId = t.ChunkId, rawText = t.RawText })
            .ToArray();

        return Results.Ok(new { session, chunks = sessionChunks, transcriptionDurations, transcriptionTexts });
    }

    private static async Task<IResult> DeleteSession(
        string id,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var session = await sessions.GetByIdAsync(id);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        await sessions.DeleteAsync(id);
        return Results.NoContent();
    }

    private static async Task<IResult> RenameSpeaker(
        string id,
        RenameSpeakerRequest request,
        ISessionRepository sessions,
        ITranscriptionRepository transcriptions,
        IMemoRepository memos,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.OldName) || string.IsNullOrWhiteSpace(request.NewName))
            return Results.BadRequest(new { error = "OldName and NewName are required." });

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var session = await sessions.GetByIdAsync(id);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        await transcriptions.ReplaceSpeakerInSessionAsync(id, request.OldName, request.NewName);
        await memos.ReplaceSpeakerAsync(id, request.OldName, request.NewName);

        return Results.Ok(new { replaced = true });
    }

    private sealed record RenameSpeakerRequest(string OldName, string NewName);
}
