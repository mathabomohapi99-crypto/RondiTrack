namespace RondiTrack.Domain;

public sealed class ContributionCycle
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public int CycleNumber { get; private set; }
    public decimal TargetAmount { get; private set; }
    public DateTime CreatedAtUtc { get; }

    public ContributionCycle(Guid stokvelId, int cycleNumber, decimal targetAmount)
    {
        if (cycleNumber < 1)
            throw new DomainValidationException("Cycle number must be 1 or greater.");
        if (targetAmount <= 0)
            throw new DomainValidationException("Target amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleNumber = cycleNumber;
        TargetAmount = targetAmount;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDetails(int cycleNumber, decimal targetAmount)
    {
        if (cycleNumber < 1)
            throw new DomainValidationException("Cycle number must be 1 or greater.");
        if (targetAmount <= 0)
            throw new DomainValidationException("Target amount must be greater than zero.");

        CycleNumber = cycleNumber;
        TargetAmount = targetAmount;
    }
}