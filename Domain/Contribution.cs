namespace RondiTrack.Domain;

public sealed class Contribution
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public Guid CycleId { get; }
    public decimal Amount { get; }
    public DateTime RecordedAtUtc { get; }

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