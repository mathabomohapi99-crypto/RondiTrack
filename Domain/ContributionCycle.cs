namespace RondiTrack.Domain;

public sealed class ContributionCycle
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public int CycleNumber { get; private set; }
    public decimal TargetAmount { get; private set; }
    public CycleStatus Status { get; private set; } = CycleStatus.Open;
    public DateTime CreatedAtUtc { get; private set; }

    // ADDED 5.3: PostgreSQL's built-in row version (xmin). Not a real column.
    public uint Version { get; private set; }

    // 6.4: last day of the cycle, as a calendar date in the stokvel's time zone (Africa/Johannesburg). null = no end date, job ignores it
    // CHANGED: moved up inside the class (it was outside the class before, which broke the build)
    public DateOnly? EndsOn { get; private set; }

    // 6.4: set ONCE by a conditional UPDATE ... WHERE ClosedAtUtc IS NULL. This is the "exactly once" marker.
    // CHANGED: moved up inside the class
    public DateTime? ClosedAtUtc { get; private set; }

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

    // 6.4: constructor for the job. Time is passed in, so nothing here touches DateTime.UtcNow
    // CHANGED: moved up inside the class
    public ContributionCycle(Guid stokvelId, int cycleNumber, decimal targetAmount, DateOnly endsOn, DateTime createdAtUtc)
    {
        if (cycleNumber < 1) throw new DomainValidationException("Cycle number must be 1 or greater.");
        if (targetAmount <= 0) throw new DomainValidationException("Target amount must be greater than zero.");
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleNumber = cycleNumber;
        TargetAmount = targetAmount;
        EndsOn = endsOn;
        CreatedAtUtc = createdAtUtc;
    }

    // ADDED 5.3: "the client last saw THIS version". Called before saving an update from a PUT.
    public void SetExpectedVersion(uint version) => Version = version;

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