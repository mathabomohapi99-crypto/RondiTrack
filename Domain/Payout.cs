namespace RondiTrack.Domain;

public sealed class Payout
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid RecipientMemberId { get; private set; } // a StokvelMember.Id
    public decimal Amount { get; private set; }
    public DateTime PaidAtUtc { get; private set; }

    private Payout() { } // for EF Core only

    public Payout(Guid stokvelId, Guid cycleId, Guid recipientMemberId, decimal amount)
    {
        if (amount <= 0)
            throw new DomainValidationException("Payout amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleId = cycleId;
        RecipientMemberId = recipientMemberId;
        Amount = amount;
        PaidAtUtc = DateTime.UtcNow;
    }
}