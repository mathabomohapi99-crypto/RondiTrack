namespace RondiTrack.Dtos;

// CHANGED: the allow-list of everything a client may send to the contributions list endpoint.
// Anything not in this class is ignored; an unknown "sort" value is rejected with 400.
public class ContributionListQuery
{
    public int? PageSize { get; set; }
    public string? PageToken { get; set; }
    public string? Sort { get; set; }        // date | -date | amount | -amount   (default: date)
    public Guid? UserId { get; set; }        // filter: one member
    public DateTime? DateFrom { get; set; }  // filter: recorded at or after (inclusive)
    public DateTime? DateTo { get; set; }    // filter: recorded at or before (inclusive)
}