namespace RondiTrack.Domain;

// NEW 9.1: one invite code for one stokvel. A signed-in person with the code can preview it and join.
public sealed class Invite
{
    public const int CodeLength = 8;

    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public string Code { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }   // a revoked code behaves like an expired one

    private Invite() { }   // for EF only

    // time comes in as a parameter, so tests can make an old invite
    public Invite(Guid stokvelId, Guid createdByUserId, string code, DateTime nowUtc, TimeSpan lifetime)
    {
        if (code.Length != CodeLength) throw new DomainValidationException("Invite code has the wrong length.");
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        Code = code;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc.Add(lifetime);
    }

    public bool IsUsableAt(DateTime nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;
}