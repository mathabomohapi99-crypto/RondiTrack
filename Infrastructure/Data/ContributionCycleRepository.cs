using System.Collections.Concurrent;
using RondiTrack.Domain;   // IContributionCycleRepository now comes from Domain

namespace RondiTrack.Data;

// interface removed: it now lives in Domain/Interfaces/IContributionCycleRepository.cs
public sealed class InMemoryContributionCycleRepository : IContributionCycleRepository
{
    private readonly ConcurrentDictionary<Guid, ContributionCycle> _cycles = new();

    public Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId) =>
        Task.FromResult<IReadOnlyList<ContributionCycle>>(
            _cycles.Values.Where(c => c.StokvelId == stokvelId).OrderBy(c => c.CycleNumber).ToList());

    public Task<ContributionCycle?> GetByIdAsync(Guid id)
    {
        _cycles.TryGetValue(id, out var cycle);
        return Task.FromResult<ContributionCycle?>(cycle);
    }

    public Task AddAsync(ContributionCycle cycle)
    {
        _cycles[cycle.Id] = cycle;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ContributionCycle cycle)
    {
        _cycles[cycle.Id] = cycle;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(_cycles.TryRemove(id, out _));
}