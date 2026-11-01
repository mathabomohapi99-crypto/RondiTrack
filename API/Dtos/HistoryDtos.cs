namespace RondiTrack.Dtos;

// NEW 8.3: everything a client may send to GET /stokvels/{id}/contributions
public class StokvelContributionListQuery
{
    public int? PageSize { get; set; }
    public string? PageToken { get; set; }
    public string? Sort { get; set; }    // -date (default) | date | amount | -amount
    public Guid? Member { get; set; }    // one member. A member who is not in this stokvel just gives an empty page
    public string? From { get; set; }    // UTC instant, INCLUDED, e.g. 2026-09-30T22:00:00Z
    public string? To { get; set; }      // UTC instant, EXCLUDED, e.g. 2026-10-31T22:00:00Z
}

// NEW 8.3: one row of the activity feed. Kind = Contribution | Payout | MemberJoined
public sealed record ActivityItemResponse(
    Guid Id,
    string Kind,
    Guid StokvelId,
    string StokvelName,
    string MemberName,
    long? AmountCents,
    DateTime OccurredAtUtc);