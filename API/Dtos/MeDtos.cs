using RondiTrack.Domain;

namespace RondiTrack.Dtos;

// NEW 8.1: who is signed in (used by the Next.js server after login)
public sealed record MeResponse(Guid Id, string FullName, string Email);

// NEW 8.1: summary of the stokvel's current (open) cycle, already worked out by the API
// NextPayoutDateUtc = when the cycle started + one period (weekly 7 days, fortnightly 14 days, monthly 1 month)
public sealed record CycleSummaryResponse(
    Guid Id,
    int CycleNumber,
    decimal TargetAmount,
    decimal CollectedAmount,
    DateTime StartedAtUtc,
    DateTime NextPayoutDateUtc);

// NEW 8.1: one row of "my stokvels": includes MY role in it and what I paid / still owe this cycle
public sealed record MyStokvelResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    ContributionFrequency Frequency,
    int MemberCount,
    StokvelRole MyRole,
    decimal PaidThisCycle,
    decimal OwedThisCycle,
    DateTime? NextPayoutDateUtc,
    CycleSummaryResponse? CurrentCycle);

// NEW 8.1: stokvel detail = the old StokvelResponse fields + my role + current cycle summary
// MyRole is null only for a platform admin who is not a member
public sealed record StokvelDetailResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    ContributionFrequency Frequency,
    int MaxMembers,
    int MemberCount,
    bool IsFull,
    decimal PayoutPerCycle,
    DateTime CreatedAtUtc,
    StokvelRole? MyRole,
    CycleSummaryResponse? CurrentCycle);