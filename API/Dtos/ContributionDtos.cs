using RondiTrack.Domain;

namespace RondiTrack.Dtos;

public sealed record ContributionRequest(Guid UserId, Guid CycleId, decimal Amount);

public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    Guid CycleId,
    decimal Amount,
    DateTime RecordedAtUtc);

// CHANGED 7.2: CycleId added, Amount is now integer cents (AmountCents), Status added, MemberEmail removed (nothing extra)
public sealed record ContributionDetailResponse(
    Guid Id,
    Guid CycleId,
    Guid UserId,
    string MemberName,
    StokvelRole Role,
    int RotationPosition,
    long AmountCents,
    ContributionStatus Status,
    DateTime RecordedAtUtc);