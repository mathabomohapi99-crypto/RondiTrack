namespace RondiTrack.Domain;

public sealed class Payout
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid CycleId { get; private set; }
    // EDIT 5.2: was RecipientMemberId (a StokvelMember.Id that no longer exists).
    // Together with StokvelId this identifies one membership.
    public Guid RecipientUserId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaidAtUtc { get; private set; }

    // ADDED 5.3: PostgreSQL's built-in row version (xmin). Not a real column, EF reads it from the system column.
    public uint Version { get; private set; }

    private Payout() { } // for EF Core only

    public Payout(Guid stokvelId, Guid cycleId, Guid recipientUserId, decimal amount)
    {
        if (amount <= 0)
            throw new DomainValidationException("Payout amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleId = cycleId;
        RecipientUserId = recipientUserId;
        Amount = amount;
        PaidAtUtc = DateTime.UtcNow;
    }
}