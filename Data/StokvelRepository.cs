using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IStokvelRepository
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync();
    Task<Stokvel?> GetByIdAsync(Guid id);
    Task<bool> AnyWithMemberAsync(Guid userId);
    Task AddAsync(Stokvel stokvel);
    Task UpdateAsync(Stokvel stokvel);
    Task<bool> DeleteAsync(Guid id);
}

public sealed class InMemoryStokvelRepository : IStokvelRepository
{
    private readonly ConcurrentDictionary<Guid, Stokvel> _stokvels = new();

    public InMemoryStokvelRepository()
    {
        foreach (var stokvel in SeedData.Stokvels)
            _stokvels[stokvel.Id] = stokvel;
    }

    public Task<IReadOnlyList<Stokvel>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Stokvel>>(_stokvels.Values.OrderBy(s => s.Name).ToList());

    public Task<Stokvel?> GetByIdAsync(Guid id)
    {
        _stokvels.TryGetValue(id, out var stokvel);
        return Task.FromResult<Stokvel?>(stokvel);
    }

    public Task<bool> AnyWithMemberAsync(Guid userId) =>
        Task.FromResult(_stokvels.Values.Any(s => s.HasMember(userId)));

    public Task AddAsync(Stokvel stokvel)
    {
        _stokvels[stokvel.Id] = stokvel;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Stokvel stokvel)
    {
        _stokvels[stokvel.Id] = stokvel;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(_stokvels.TryRemove(id, out _));
}