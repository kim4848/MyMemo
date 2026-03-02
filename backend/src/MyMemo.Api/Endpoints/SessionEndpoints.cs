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

        var session = await sessions.CreateAsync(user.Id, request.OutputMode, request.AudioSource);
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

        return Results.Ok(new { session, chunks = sessionChunks, transcriptionDurations });
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
}
