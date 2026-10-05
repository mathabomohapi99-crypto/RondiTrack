namespace RondiTrack.Api.Common.Paging;

public static class PagingDefaults
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    // NEW: page size rules.
    // null or 0  -> use the default
    // negative   -> 400
    // too big    -> quietly reduced to the maximum (not rejected)
    public static int Resolve(int? requested)
    {
        if (requested is null || requested == 0) return DefaultPageSize;
        if (requested < 0) throw new ApiBadRequestException("pageSize cannot be negative.");
        return Math.Min(requested.Value, MaxPageSize);
    }
}