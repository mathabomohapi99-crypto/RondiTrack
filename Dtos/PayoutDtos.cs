namespace RondiTrack.Dtos;

public sealed record PayoutResponse(
    Guid Id,
    Guid StokvelId,
    Guid CycleId,
    Guid RecipientMemberId,
    decimal Amount,
    DateTime PaidAtUtc);