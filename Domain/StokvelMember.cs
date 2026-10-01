namespace RondiTrack.Domain;

public sealed class StokvelMember
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public int RotationPosition { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }

    private StokvelMember() { } // for EF Core only

    public StokvelMember(Guid stokvelId, Guid userId, int rotationPosition)
    {
        if (rotationPosition < 1)
            throw new DomainValidationException("Rotation position must be 1 or higher.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        RotationPosition = rotationPosition;
        JoinedAtUtc = DateTime.UtcNow;
    }
}