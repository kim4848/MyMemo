using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class UserRepository(IDbConnectionFactory db) : IUserRepository
{
    public async Task<User> GetOrCreateByClerkIdAsync(string clerkId, string email, string name)
    {
        using var conn = await db.CreateConnectionAsync();
        const string selectSql = "SELECT id AS Id, email AS Email, name AS Name, clerk_id AS ClerkId, created_at AS CreatedAt, updated_at AS UpdatedAt FROM users";

        var existing = await conn.QuerySingleOrDefaultAsync<User>(
            $"{selectSql} WHERE clerk_id = @clerkId", new { clerkId });
        if (existing is not null)
            return existing;

        // Email might exist with a different clerk_id (e.g. dev DB was recreated)
        existing = await conn.QuerySingleOrDefaultAsync<User>(
            $"{selectSql} WHERE email = @email", new { email });
        if (existing is not null)
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            await conn.ExecuteAsync(
                "UPDATE users SET clerk_id = @clerkId, name = @name, updated_at = @now WHERE email = @email",
                new { clerkId, name, now, email });
            return (await conn.QuerySingleAsync<User>($"{selectSql} WHERE clerk_id = @clerkId", new { clerkId }));
        }

        {
            var id = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            await conn.ExecuteAsync(
                "INSERT OR IGNORE INTO users (id, email, name, clerk_id, created_at, updated_at) VALUES (@id, @email, @name, @clerkId, @now, @now)",
                new { id, email, name, clerkId, now });
            return await conn.QuerySingleAsync<User>($"{selectSql} WHERE clerk_id = @clerkId", new { clerkId });
        }
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT id AS Id, email AS Email, name AS Name, clerk_id AS ClerkId, created_at AS CreatedAt, updated_at AS UpdatedAt FROM users WHERE id = @id",
            new { id });
    }
}
