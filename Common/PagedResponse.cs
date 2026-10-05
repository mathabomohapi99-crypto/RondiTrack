namespace RondiTrack.Api.Common.Paging;

// NEW: the shape every paged endpoint returns.
// NextPageToken is "" when there are no more results (end-of-results signal).
// There is no total count on purpose (see README).
public record PagedResponse<T>(IReadOnlyList<T> Items, string NextPageToken);