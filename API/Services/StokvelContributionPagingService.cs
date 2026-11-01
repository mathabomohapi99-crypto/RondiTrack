using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// NEW 8.3: contribution history of ONE stokvel (all cycles), filtered and paged in the database.
// Time range = [from, to): from INCLUDED, to EXCLUDED, both UTC instants. The API never converts days.
public sealed class StokvelContributionPagingService(RondiTrackDbContext db)
{
    private static readonly string[] AllowedSorts = ["date", "-date", "amount", "-amount"];

    public async Task<PagedResponse<ContributionDetailResponse>> ListAsync(
        Guid stokvelId, StokvelContributionListQuery q, CancellationToken ct)
    {
        var pageSize = PagingDefaults.Resolve(q.PageSize);

        var sort = string.IsNullOrWhiteSpace(q.Sort) ? "-date" : q.Sort.Trim().ToLowerInvariant();
        if (!AllowedSorts.Contains(sort))
            throw new ApiBadRequestException($"Unknown sort '{q.Sort}'. Allowed: {string.Join(", ", AllowedSorts)}.");

        var from = ParseInstant(q.From, "from");
        var to = ParseInstant(q.To, "to");
        if (from.HasValue && to.HasValue && from >= to)
            throw new ApiBadRequestException("from must be before to.");

        IQueryable<Contribution> query = db.Contributions
            .AsNoTracking()
            .Where(c => c.StokvelId == stokvelId);

        if (q.Member.HasValue)
        {
            var m = q.Member.Value;
            query = query.Where(c => c.UserId == m);   // someone from another stokvel simply matches nothing
        }
        if (from.HasValue)
        {
            var f = from.Value;
            query = query.Where(c => c.RecordedAtUtc >= f);   // from is INCLUDED
        }
        if (to.HasValue)
        {
            var t = to.Value;
            query = query.Where(c => c.RecordedAtUtc < t);    // to is EXCLUDED
        }

        // A page token only fits the same sort + filters (otherwise 400, never silently skipped rows)
        var context = PageTokenCodec.ContextOf(
            "stokvel", stokvelId, sort, q.Member, from?.ToString("O"), to?.ToString("O"));

        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var cursor = PageTokenCodec.Decode(q.PageToken, context);
            query = ApplyAfterCursor(query, sort, cursor);
        }

        query = ApplyOrder(query, sort);

        // Ask for ONE extra row. If it comes back, there is a next page.
        var rows = await query
            .Take(pageSize + 1)
            .Select(c => new
            {
                c.Id, c.CycleId, c.UserId,
                MemberName = c.Member.User.FullName,
                c.Member.Role, c.Member.RotationPosition,
                c.Amount, c.Status, c.RecordedAtUtc
            })
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        var pageRows = rows.Take(pageSize).ToList();

        var next = "";
        if (hasMore)
        {
            var last = pageRows[^1];
            var lastValue = sort.EndsWith("date")
                ? last.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture)
                : last.Amount.ToString(CultureInfo.InvariantCulture);
            next = PageTokenCodec.Encode(new PageCursor(context, lastValue, last.Id));
        }

        var page = pageRows.Select(r => new ContributionDetailResponse(
            r.Id, r.CycleId, r.UserId, r.MemberName, r.Role, r.RotationPosition,
            Money.ToCents(r.Amount), r.Status, r.RecordedAtUtc)).ToList();

        return new PagedResponse<ContributionDetailResponse>(page, next);
    }

    // A malformed instant is a 400 like every other bad input
    private static DateTime? ParseInstant(string? text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            throw new ApiBadRequestException($"{name} must be a UTC instant like 2026-09-30T22:00:00Z.");
        return parsed.UtcDateTime;
    }

    private static IQueryable<Contribution> ApplyOrder(IQueryable<Contribution> q, string sort) => sort switch
    {
        "date"   => q.OrderBy(c => c.RecordedAtUtc).ThenBy(c => c.Id),
        "-date"  => q.OrderByDescending(c => c.RecordedAtUtc).ThenByDescending(c => c.Id),
        "amount" => q.OrderBy(c => c.Amount).ThenBy(c => c.Id),
        _        => q.OrderByDescending(c => c.Amount).ThenByDescending(c => c.Id),
    };

    private static IQueryable<Contribution> ApplyAfterCursor(IQueryable<Contribution> q, string sort, PageCursor cur)
    {
        var lastId = cur.LastId;

        if (sort.EndsWith("date"))
        {
            if (!DateTime.TryParse(cur.LastValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
                throw new ApiBadRequestException("pageToken is invalid.");

            var d = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return sort == "date"
                ? q.Where(c => c.RecordedAtUtc > d || (c.RecordedAtUtc == d && c.Id.CompareTo(lastId) > 0))
                : q.Where(c => c.RecordedAtUtc < d || (c.RecordedAtUtc == d && c.Id.CompareTo(lastId) < 0));
        }

        if (!decimal.TryParse(cur.LastValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var a))
            throw new ApiBadRequestException("pageToken is invalid.");

        return sort == "amount"
            ? q.Where(c => c.Amount > a || (c.Amount == a && c.Id.CompareTo(lastId) > 0))
            : q.Where(c => c.Amount < a || (c.Amount == a && c.Id.CompareTo(lastId) < 0));
    }
}