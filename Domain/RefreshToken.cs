namespace RondiTrack.Domain;

// NEW 6.1: one refresh token. Every token from ONE login shares a FamilyId,
// so one reuse can revoke the whole chain.
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }                 // one login = one family
    public string TokenHash { get; private set; } = null!;     // only the hash is stored
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }           // set when it was rotated
    public DateTime? RevokedAtUtc { get; private set; }        // set on logout or reuse
    public Guid? ReplacedByTokenId { get; private set; }       // the token that replaced it

    private RefreshToken() { } // for EF Core only

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTime nowUtc, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
    }
}