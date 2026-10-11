using RondiTrack.Domain;

namespace RondiTrack.Dtos;

public sealed record StokvelRequest(
    string Name,
    decimal ContributionAmount,
    ContributionFrequency Frequency,
    int MaxMembers);

public sealed record StokvelResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    ContributionFrequency Frequency,
    int MaxMembers,
    int MemberCount,
    bool IsFull,
    decimal PayoutPerCycle,
    DateTime CreatedAtUtc);

public sealed record AddMemberRequest(Guid UserId);