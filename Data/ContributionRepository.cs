using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IContributionRepository
{
    Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid cycleId);
    Task AddAsync(Contribution contribution);
    Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId);
}

public sealed class InMemoryContributionRepository : IContributionRepository
{
    private readonly ConcurrentBag<Contribution> _contributions = [];

    public Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid cycleId) =>
        Task.FromResult(_contributions.Any(c =>
            c.StokvelId == stokvelId && c.UserId == userId && c.CycleId == cycleId));

    public Task AddAsync(Contribution contribution)
    {
        _contributions.Add(contribution);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId) =>
        Task.FromResult<IReadOnlyList<Contribution>>(
            _contributions.Where(c => c.StokvelId == stokvelId).ToList());
}