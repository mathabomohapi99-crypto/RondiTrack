namespace RondiTrack.Dtos;

public sealed record PayoutResponse(
    Guid Id,
    Guid StokvelId,
    Guid CycleId,
    Guid RecipientUserId,   // Was RecipientMemberId
    decimal Amount,
    DateTime PaidAtUtc);