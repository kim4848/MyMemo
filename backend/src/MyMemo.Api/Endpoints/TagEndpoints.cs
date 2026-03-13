using System.Security.Claims;
using MyMemo.Shared.Repositories;

namespace MyMemo.Api.Endpoints;

public static class TagEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tags").RequireAuthorization();

        group.MapGet("", ListTags);
        group.MapPost("", CreateTag);
        group.MapPut("{id}", UpdateTag);
        group.MapDelete("{id}", DeleteTag);

        var sessionTags = app.MapGroup("/api/sessions/{sessionId}/tags").RequireAuthorization();
        sessionTags.MapGet("", GetSessionTags);
        sessionTags.MapPost("{tagId}", AddTagToSession);
        sessionTags.MapDelete("{tagId}", RemoveTagFromSession);
    }

    private static async Task<IResult> ListTags(
        ITagRepository tags,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var list = await tags.ListByUserAsync(user.Id);
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateTag(
        CreateTagRequest request,
        ITagRepository tags,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var tag = await tags.CreateAsync(user.Id, request.Name.Trim(), request.Color);
        return Results.Created($"/api/tags/{tag.Id}", tag);
    }

    private static async Task<IResult> UpdateTag(
        string id,
        UpdateTagRequest request,
        ITagRepository tags,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        await tags.UpdateAsync(id, request.Name.Trim(), request.Color);
        return Results.Ok();
    }

    private static async Task<IResult> DeleteTag(
        string id,
        ITagRepository tags,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        await tags.DeleteAsync(id);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSessionTags(
        string sessionId,
        ITagRepository tags,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id) return Results.NotFound();

        var list = await tags.GetTagsForSessionAsync(sessionId);
        return Results.Ok(list);
    }

    private static async Task<IResult> AddTagToSession(
        string sessionId,
        string tagId,
        ITagRepository tags,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id) return Results.NotFound();

        await tags.AddTagToSessionAsync(sessionId, tagId);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveTagFromSession(
        string sessionId,
        string tagId,
        ITagRepository tags,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id) return Results.NotFound();

        await tags.RemoveTagFromSessionAsync(sessionId, tagId);
        return Results.NoContent();
    }

    private sealed record CreateTagRequest(string Name, string? Color = null);
    private sealed record UpdateTagRequest(string Name, string? Color = null);
}
