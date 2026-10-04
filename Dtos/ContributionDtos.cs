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

// New. The shape the contributions-by-cycle endpoint returns
public sealed record ContributionDetailResponse(
    Guid Id,
    Guid UserId,
    string MemberName,
    string MemberEmail,
    StokvelRole Role,
    int RotationPosition,
    decimal Amount,
    DateTime RecordedAtUtc);