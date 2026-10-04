namespace RondiTrack.Domain;

public sealed class ContributionCycle
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public int CycleNumber { get; private set; }
    public decimal TargetAmount { get; private set; }
    public CycleStatus Status { get; private set; } = CycleStatus.Open;
    public DateTime CreatedAtUtc { get; private set; }

    // Real navigation (the second one-to-many: Stokvel -> ContributionCycle)
    public Stokvel Stokvel { get; private set; } = null!;

    private ContributionCycle() { } // for EF Core only

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

    public void MarkPaidOut()
    {
        if (Status != CycleStatus.Open)
            throw new DomainConflictException("This cycle has already been paid out.");

        Status = CycleStatus.PaidOut;
    }
}