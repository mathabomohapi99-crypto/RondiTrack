namespace RondiTrack.Domain;   // same namespace as the entities

public interface IContributionRepository   // moved here from Infrastructure/Data/ContributionRepository.cs
{
    Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid cycleId);
    Task AddAsync(Contribution contribution);
    Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId);
}