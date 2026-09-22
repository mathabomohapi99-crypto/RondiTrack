namespace RondiTrack.Domain;

public sealed class Stokvel
{
    public const int MaxNameLength = 100;
    public const int MinMembers = 2;
    public const int MaxMembersLimit = 50;
    public const decimal MaxContribution = 1_000_000m;

    private readonly List<User> _members = [];

    public Guid Id { get; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public ContributionFrequency Frequency { get; private set; }
    public int MaxMembers { get; private set; }
    public DateTime CreatedAtUtc { get; }

    public IReadOnlyCollection<User> Members => _members.AsReadOnly();
    public decimal PayoutPerCycle => ContributionAmount * _members.Count;
    public bool IsFull => _members.Count >= MaxMembers;

    public Stokvel(string name, decimal contributionAmount, ContributionFrequency frequency, int maxMembers)
    {
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTime.UtcNow;
        Name = ValidateName(name);
        ContributionAmount = ValidateAmount(contributionAmount);
        Frequency = ValidateFrequency(frequency);
        MaxMembers = ValidateMaxMembers(maxMembers);
    }

    internal Stokvel(Guid id, string name, decimal contributionAmount,
        ContributionFrequency frequency, int maxMembers)
        : this(name, contributionAmount, frequency, maxMembers)
    {
        Id = id;
    }

    public void UpdateDetails(string name, decimal contributionAmount,
        ContributionFrequency frequency, int maxMembers)
    {
        var validName = ValidateName(name);
        var validAmount = ValidateAmount(contributionAmount);
        var validFrequency = ValidateFrequency(frequency);
        var validMax = ValidateMaxMembers(maxMembers);

        if (validMax < _members.Count)
            throw new DomainConflictException(
                $"Cannot reduce capacity to {validMax}: '{validName}' already has {_members.Count} members.");

        Name = validName;
        ContributionAmount = validAmount;
        Frequency = validFrequency;
        MaxMembers = validMax;
    }

    public void AddMember(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (HasMember(user.Id))
            throw new DomainConflictException($"{user.FullName} is already a member of '{Name}'.");

        if (IsFull)
            throw new DomainConflictException($"'{Name}' is full ({MaxMembers} members).");

        _members.Add(user);
    }

    public bool RemoveMember(Guid userId) => _members.RemoveAll(m => m.Id == userId) > 0;

    public bool HasMember(Guid userId) => _members.Any(m => m.Id == userId);

    public User? FindMember(Guid userId) => _members.FirstOrDefault(m => m.Id == userId);

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Stokvel name is required.");

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new DomainValidationException($"Stokvel name cannot exceed {MaxNameLength} characters.");

        return trimmed;
    }

    private static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new DomainValidationException("Contribution amount must be greater than zero.");

        if (amount > MaxContribution)
            throw new DomainValidationException($"Contribution amount cannot exceed R{MaxContribution:N0}.");

        if (decimal.Round(amount, 2) != amount)
            throw new DomainValidationException("Contribution amount cannot have more than 2 decimal places.");

        return amount;
    }

    private static ContributionFrequency ValidateFrequency(ContributionFrequency frequency)
    {
        if (!Enum.IsDefined(frequency))
            throw new DomainValidationException("Frequency must be Weekly, Fortnightly or Monthly.");

        return frequency;
    }

    private static int ValidateMaxMembers(int maxMembers)
    {
        if (maxMembers < MinMembers || maxMembers > MaxMembersLimit)
            throw new DomainValidationException(
                $"Max members must be between {MinMembers} and {MaxMembersLimit}.");

        return maxMembers;
    }
}