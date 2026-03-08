using System.Diagnostics;
using System.Security.Claims;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Api.Endpoints;

public static class InfographicEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions/{sessionId}/infographic", GenerateInfographic)
            .RequireAuthorization();
        app.MapGet("/api/sessions/{sessionId}/infographic", GetInfographic)
            .RequireAuthorization();
        app.MapDelete("/api/sessions/{sessionId}/infographic", DeleteInfographic)
            .RequireAuthorization();
    }

    private static async Task<IResult> GenerateInfographic(
        string sessionId,
        ISessionRepository sessions,
        IMemoRepository memos,
        IInfographicRepository infographics,
        IInfographicService infographicService,
        IUserRepository users,
        ILogger<Program> logger,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var memo = await memos.GetBySessionIdAsync(sessionId);
        if (memo is null)
            return Results.BadRequest(new { error = "No memo available. Generate a memo first." });

        // Delete existing infographic if regenerating
        await infographics.DeleteBySessionIdAsync(sessionId);

        try
        {
            var sw = Stopwatch.StartNew();
            var result = await infographicService.GenerateAsync(memo.Content, memo.OutputMode);
            sw.Stop();

            await infographics.CreateAsync(
                sessionId,
                result.SvgContent,
                result.ModelUsed,
                result.PromptTokens,
                result.CompletionTokens,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Infographic generation failed for session {SessionId}", sessionId);
            return Results.Json(
                new { error = "Infographic generation failed. Please try again later." },
                statusCode: 502);
        }

        var infographic = await infographics.GetBySessionIdAsync(sessionId);
        return Results.Ok(infographic);
    }

    private static async Task<IResult> GetInfographic(
        string sessionId,
        ISessionRepository sessions,
        IInfographicRepository infographics,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var infographic = await infographics.GetBySessionIdAsync(sessionId);
        if (infographic is null) return Results.NotFound();

        return Results.Ok(infographic);
    }

    private static async Task<IResult> DeleteInfographic(
        string sessionId,
        ISessionRepository sessions,
        IInfographicRepository infographics,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        await infographics.DeleteBySessionIdAsync(sessionId);
        return Results.NoContent();
    }
}
