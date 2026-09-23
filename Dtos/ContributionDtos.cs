namespace RondiTrack.Dtos;

public sealed record ContributionRequest(Guid UserId, int Cycle, decimal Amount);

public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    int Cycle,
    decimal Amount,
    DateTime RecordedAtUtc);