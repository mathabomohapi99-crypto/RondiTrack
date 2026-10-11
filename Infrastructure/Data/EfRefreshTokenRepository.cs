using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

// NEW 6.1: refresh tokens in PostgreSQL
public sealed class EfRefreshTokenRepository(RondiTrackDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();
    }

    // pure read, so not tracked
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash) =>
        await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    // One UPDATE with a WHERE that only matches an unused, unrevoked token.
    // If two requests race, only one gets "1 row changed"; the other gets 0 and is treated as reuse.
    public async Task<bool> TryMarkUsedAsync(Guid tokenId, Guid replacedByTokenId, DateTime nowUtc)
    {
        var rows = await db.RefreshTokens
            .Where(t => t.Id == tokenId && t.UsedAtUtc == null && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.UsedAtUtc, (DateTime?)nowUtc)
                .SetProperty(t => t.ReplacedByTokenId, (Guid?)replacedByTokenId));
        return rows == 1;
    }

    // Revokes every not-yet-revoked token of this login (the whole family)
    public async Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc) =>
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)nowUtc));
}