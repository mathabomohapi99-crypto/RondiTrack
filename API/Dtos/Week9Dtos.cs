using RondiTrack.Domain;

namespace RondiTrack.Dtos;

// NEW 9.1: invites
public sealed record InviteResponse(string Code, DateTime ExpiresAtUtc);

// The preview for someone who is NOT a member yet: no member names, no balances, no payout order.
// StokvelId is filled ONLY when the caller is already a member (so the page can link to it).
public sealed record InvitePreviewResponse(
    string StokvelName,
    long ContributionAmountCents,
    ContributionFrequency Frequency,
    int MemberCount,
    int MaxMembers,
    DateTime ExpiresAtUtc,
    bool AlreadyMember,
    Guid? StokvelId);

public sealed record JoinInviteResponse(Guid StokvelId);

// NEW 9.1: proof of payment (never contains the file bytes)
public sealed record ProofResponse(
    Guid ContributionId, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc, bool Locked);

// NEW 9.1: close preview and close
public sealed record ArrearLineResponse(Guid UserId, string MemberName, long ShortfallCents);

public sealed record ClosePreviewResponse(
    Guid CycleId,
    int CycleNumber,
    string AsOfDate,                  // the South African date, yyyy-MM-dd
    long DueCentsPerMember,
    long TotalArrearsCents,
    IReadOnlyList<ArrearLineResponse> Arrears,
    string Version);                  // changes whenever the numbers above would change

public sealed record CloseCycleRequest(string Version);

public sealed record CloseCycleResponse(
    Guid CycleId,
    DateTime ClosedAtUtc,
    int ArrearsRecorded,
    long TotalArrearsCents,
    IReadOnlyList<ArrearLineResponse> Arrears);