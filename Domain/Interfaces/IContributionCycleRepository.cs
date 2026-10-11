namespace RondiTrack.Domain;   // same namespace as the entities

public interface IContributionCycleRepository   // moved here from Infrastructure/Data/ContributionCycleRepository.cs
{
    Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId);
    Task<ContributionCycle?> GetByIdAsync(Guid id);
    Task AddAsync(ContributionCycle cycle);
    Task UpdateAsync(ContributionCycle cycle);
    Task<bool> DeleteAsync(Guid id);
}