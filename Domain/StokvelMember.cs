namespace RondiTrack.Domain;

public sealed class StokvelMember
{
    // the old "Guid Id" is GONE. The key is now the pair (UserId, StokvelId)
    public Guid UserId { get; private set; }
    public Guid StokvelId { get; private set; }
    public StokvelRole Role { get; private set; }            // EDIT 5.2: new
    public int RotationPosition { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }

    // real navigations
    public User User { get; private set; } = null!;
    public Stokvel Stokvel { get; private set; } = null!;
    public List<Contribution> Contributions { get; } = [];

    private StokvelMember() { } // for EF Core only

    public StokvelMember(Guid stokvelId, Guid userId, int rotationPosition,
        StokvelRole role = StokvelRole.Member)
    {
        if (rotationPosition < 1)
            throw new DomainValidationException("Rotation position must be 1 or higher.");

        StokvelId = stokvelId;
        UserId = userId;
        RotationPosition = rotationPosition;
        Role = role;
        JoinedAtUtc = DateTime.UtcNow;
    }
}