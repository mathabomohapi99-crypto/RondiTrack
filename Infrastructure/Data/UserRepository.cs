using System.Collections.Concurrent;
using RondiTrack.Domain;   // IUserRepository now comes from Domain

namespace RondiTrack.Data;

// interface removed: it now lives in Domain/Interfaces/IUserRepository.cs
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

    // NEW 6.1: needed because the interface has a new method
    public Task<User?> GetByEmailAsync(string email)
    {
        var normalized = email.Trim();
        var user = _users.Values.FirstOrDefault(u =>
            string.Equals(u.Email, normalized, StringComparison.OrdinalIgnoreCase));
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