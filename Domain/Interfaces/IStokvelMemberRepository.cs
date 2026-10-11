namespace RondiTrack.Domain;   // same namespace as the entities

public interface IStokvelMemberRepository   // moved here from Infrastructure/Data/StokvelMemberRepository.cs
{
    // forUpdate: false = pure read (not tracked), true = I will change it and save
    Task<StokvelMember?> GetAsync(Guid userId, Guid stokvelId, bool forUpdate = false);
    Task<IReadOnlyList<StokvelMember>> GetByStokvelAsync(Guid stokvelId);
    Task AddAsync(StokvelMember member);
    Task RemoveAsync(StokvelMember member);
}