using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// CHANGED 7.2: the rows now carry CycleId, integer cents and Status, and no email.
public sealed class ContributionPagingService(RondiTrackDbContext db)
{
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

        var context = PageTokenCodec.ContextOf(
            sort, cycleId, q.UserId, from?.ToString("O"), to?.ToString("O"));

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

        // money becomes integer cents here
        var page = pageRows.Select(r => new ContributionDetailResponse(
            r.Id, r.CycleId, r.UserId, r.MemberName, r.Role, r.RotationPosition,
            Money.ToCents(r.Amount), r.Status, r.RecordedAtUtc)).ToList();

        return new PagedResponse<ContributionDetailResponse>(page, next);
    }

    private static IQueryable<Contribution> ApplyOrder(IQueryable<Contribution> q, string sort) => sort switch
    {
        "date"   => q.OrderBy(c => c.RecordedAtUtc).ThenBy(c => c.Id),
        "-date"  => q.OrderByDescending(c => c.RecordedAtUtc).ThenByDescending(c => c.Id),
        "amount" => q.OrderBy(c => c.Amount).ThenBy(c => c.Id),
        _        => q.OrderByDescending(c => c.Amount).ThenByDescending(c => c.Id),
    };

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

    private static DateTime? ToUtc(DateTime? value) => value is null ? null
        : value.Value.Kind == DateTimeKind.Utc ? value
        : value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime()
        : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}