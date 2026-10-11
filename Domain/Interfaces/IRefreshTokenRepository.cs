namespace RondiTrack.Domain;

// NEW 6.1: refresh tokens live in PostgreSQL, not in memory
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    // true only if THIS call marked it used (a single atomic UPDATE, so two requests cannot both win)
    Task<bool> TryMarkUsedAsync(Guid tokenId, Guid replacedByTokenId, DateTime nowUtc);

    // revokes every token that came from the same login
    Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc);
}