using System.Collections.Concurrent;
using RondiTrack.Domain;   // IContributionRepository now comes from Domain

namespace RondiTrack.Data;

// interface removed: it now lives in Domain/Interfaces/IContributionRepository.cs
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