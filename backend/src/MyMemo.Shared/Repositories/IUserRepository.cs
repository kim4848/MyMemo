using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IUserRepository
{
    Task<User> GetOrCreateByClerkIdAsync(string clerkId, string email, string name);
    Task<User?> GetByIdAsync(string id);
}
