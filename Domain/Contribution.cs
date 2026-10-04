namespace RondiTrack.Domain;

public sealed class Contribution
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CycleId { get; private set; }   // stays a bare Guid on purpose (a stated "not yet")
    public decimal Amount { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    // Points at ONE specific membership (composite foreign key UserId + StokvelId)
    public StokvelMember Member { get; private set; } = null!;

    private Contribution() { } // for EF Core only

    public Contribution(Guid stokvelId, Guid userId, Guid cycleId, decimal amount)
    {
        if (amount <= 0)
            throw new DomainValidationException("Contribution amount must be greater than zero.");

        if (decimal.Round(amount, 2) != amount)
            throw new DomainValidationException("Amount cannot have more than 2 decimal places.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        CycleId = cycleId;
        Amount = amount;
        RecordedAtUtc = DateTime.UtcNow;
    }
}