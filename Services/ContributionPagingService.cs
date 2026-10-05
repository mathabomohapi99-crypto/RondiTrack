using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// CHANGED: rewritten for your real names (RondiTrackDbContext, Guid ids, RecordedAtUtc).
// The whole query is built as IQueryable, so WHERE / ORDER BY / LIMIT run inside PostgreSQL.
public sealed class ContributionPagingService(RondiTrackDbContext db)
{
    // The ONLY sorts a client may ask for. Anything else is a 400.
    private static readonly string[] AllowedSorts = ["date", "-date", "amount", "-amount"];

    public async Task<PagedResponse<ContributionDetailResponse>> ListAsync(
        Guid cycleId, ContributionListQuery q, CancellationToken ct)
    {
        var pageSize = PagingDefaults.Resolve(q.PageSize);

        var sort = string.IsNullOrWhiteSpace(q.Sort) ? "date" : q.Sort.Trim().ToLowerInvariant();
        if (!AllowedSorts.Contains(sort))
            throw new ApiBadRequestException(
                $"Unknown sort '{q.Sort}'. Allowed: {string.Join(", ", AllowedSorts)}.");

        var from = ToUtc(q.DateFrom);
        var to = ToUtc(q.DateTo);
        if (from.HasValue && to.HasValue && from > to)
            throw new ApiBadRequestException("dateFrom cannot be after dateTo.");

        IQueryable<Contribution> query = db.Contributions
            .AsNoTracking()
            .Where(c => c.CycleId == cycleId);

        if (q.UserId.HasValue)
        {
            var uid = q.UserId.Value;
            query = query.Where(c => c.UserId == uid);
        }
        if (from.HasValue)
        {
            var f = from.Value;
            query = query.Where(c => c.RecordedAtUtc >= f);
        }
        if (to.HasValue)
        {
            var t = to.Value;
            query = query.Where(c => c.RecordedAtUtc <= t);
        }

        // Fingerprint of sort + filters, stored inside every token we hand out.
        var context = PageTokenCodec.ContextOf(
            sort, cycleId, q.UserId, from?.ToString("O"), to?.ToString("O"));

        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var cursor = PageTokenCodec.Decode(q.PageToken, context);
            query = ApplyAfterCursor(query, sort, cursor);   // keyset: "rows after the last one you saw"
        }

        query = ApplyOrder(query, sort);

        // Ask for ONE extra row. If it comes back, there is a next page.
        var rows = await query
            .Take(pageSize + 1)
            .Select(c => new ContributionDetailResponse(
                c.Id, c.UserId, c.Member.User.FullName, c.Member.User.Email,
                c.Member.Role, c.Member.RotationPosition, c.Amount, c.RecordedAtUtc))
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        var next = "";
        if (hasMore)
        {
            var last = page[^1];
            var lastValue = sort.EndsWith("date")
                ? last.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture)
                : last.Amount.ToString(CultureInfo.InvariantCulture);
            next = PageTokenCodec.Encode(new PageCursor(context, lastValue, last.Id));
        }

        return new PagedResponse<ContributionDetailResponse>(page, next);
    }

    // Every sort ends with Id, so rows with equal date/amount always come in the same order.
    private static IQueryable<Contribution> ApplyOrder(IQueryable<Contribution> q, string sort) => sort switch
    {
        "date"   => q.OrderBy(c => c.RecordedAtUtc).ThenBy(c => c.Id),
        "-date"  => q.OrderByDescending(c => c.RecordedAtUtc).ThenByDescending(c => c.Id),
        "amount" => q.OrderBy(c => c.Amount).ThenBy(c => c.Id),
        _        => q.OrderByDescending(c => c.Amount).ThenByDescending(c => c.Id),
    };

    // Keyset condition: rows that come AFTER (lastValue, lastId) in the chosen order.
    private static IQueryable<Contribution> ApplyAfterCursor(
        IQueryable<Contribution> q, string sort, PageCursor cur)
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

    // npgsql only accepts UTC for timestamptz, so a date typed by a client is treated as UTC.
    private static DateTime? ToUtc(DateTime? value) => value is null ? null
        : value.Value.Kind == DateTimeKind.Utc ? value
        : value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime()
        : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}