using System.Security.Claims;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Api.Endpoints;

public static class MemoEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions/{sessionId}/finalize", FinalizeSession)
            .RequireAuthorization();
        app.MapGet("/api/sessions/{sessionId}/memo", GetMemo)
            .RequireAuthorization();
    }

    private static async Task<IResult> FinalizeSession(
        string sessionId,
        ISessionRepository sessions,
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

        await sessions.SetEndedAtAsync(sessionId);
        await sessions.UpdateStatusAsync(sessionId, "processing");
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
}
