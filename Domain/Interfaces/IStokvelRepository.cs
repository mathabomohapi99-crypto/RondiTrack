namespace RondiTrack.Domain;   // same namespace as the entities

public interface IStokvelRepository   // moved here from Infrastructure/Data/StokvelRepository.cs
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync();
    Task<Stokvel?> GetByIdAsync(Guid id);
    Task<bool> AnyWithMemberAsync(Guid userId);
    Task AddAsync(Stokvel stokvel);
    Task UpdateAsync(Stokvel stokvel);
    Task<bool> DeleteAsync(Guid id);
}
