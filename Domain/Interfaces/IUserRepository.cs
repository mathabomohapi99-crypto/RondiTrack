namespace RondiTrack.Domain;   // same namespace as the entities, so no using lines change

public interface IUserRepository   // moved here from Infrastructure/Data/UserRepository.cs
{
    Task<IReadOnlyList<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> DeleteAsync(Guid id);
}