using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> DeleteAsync(Guid id);
}

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public InMemoryUserRepository()
    {
        foreach (var user in SeedData.Users)
            _users[user.Id] = user;
    }

    public Task<IReadOnlyList<User>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<User>>(_users.Values.OrderBy(u => u.FullName).ToList());

    public Task<User?> GetByIdAsync(Guid id)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult<User?>(user);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null)
    {
        var normalized = email.Trim();
        var exists = _users.Values.Any(u =>
            u.Id != excludingUserId &&
            string.Equals(u.Email, normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(User user)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(_users.TryRemove(id, out _));
}