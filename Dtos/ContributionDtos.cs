namespace RondiTrack.Dtos;

public sealed record ContributionRequest(Guid UserId, Guid CycleId, decimal Amount);

public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    Guid CycleId,
    decimal Amount,
    DateTime RecordedAtUtc);