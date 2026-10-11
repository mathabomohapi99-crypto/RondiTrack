namespace RondiTrack.Dtos;

public sealed record ContributionCycleRequest(int CycleNumber, decimal TargetAmount);

public sealed record ContributionCycleResponse(Guid Id, Guid StokvelId, int CycleNumber, decimal TargetAmount, DateTime CreatedAtUtc);