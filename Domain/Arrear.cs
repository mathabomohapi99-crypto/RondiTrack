namespace RondiTrack.Domain;

// 6.4: money a member still owed when a cycle closed. A financial record, so it is never deleted by the jobs.
public sealed class Arrear
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid UserId { get; private set; }
    public long ShortfallCents { get; private set; }   // integer cents, exact
    public DateTime RecordedAtUtc { get; private set; }

    private Arrear() { }   // for EF only

    // 6.4: time comes in as a parameter (TimeProvider), never DateTime.UtcNow
    public Arrear(Guid stokvelId, Guid cycleId, Guid userId, long shortfallCents, DateTime recordedAtUtc)
    {
        if (shortfallCents <= 0) throw new DomainValidationException("Shortfall must be greater than zero.");
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleId = cycleId;
        UserId = userId;
        ShortfallCents = shortfallCents;
        RecordedAtUtc = recordedAtUtc;
    }
}