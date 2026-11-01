using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// NEW 8.3: recent activity (contributions, payouts, members joining) across ONLY the stokvels I belong to.
// Newest first. Each kind is read separately (newest N), then merged, so every query stays small.
public sealed class ActivityFeedService(RondiTrackDbContext db)
{
    private sealed record Row(Guid Id, string Kind, Guid StokvelId, string StokvelName,
        string MemberName, long? AmountCents, DateTime OccurredAtUtc);

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    public async Task<PagedResponse<ActivityItemResponse>> ListAsync(
        Guid userId, int? requestedPageSize, string? pageToken, CancellationToken ct)
    {
        var pageSize = PagingDefaults.Resolve(requestedPageSize);
        var take = pageSize + 1;
        var context = PageTokenCodec.ContextOf("activity", userId);

        DateTime? cursorTime = null;
        var cursorId = Guid.Empty;
        if (!string.IsNullOrEmpty(pageToken))
        {
            var cursor = PageTokenCodec.Decode(pageToken, context);
            if (!DateTime.TryParse(cursor.LastValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
                throw new ApiBadRequestException("pageToken is invalid.");
            cursorTime = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            cursorId = cursor.LastId;
        }

        // only the stokvels I am a member of: nothing else can ever appear in my feed
        var mine = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.StokvelId)
            .ToListAsync(ct);
        if (mine.Count == 0) return new PagedResponse<ActivityItemResponse>([], "");

        IQueryable<Contribution> cq = db.Contributions.AsNoTracking().Where(c => mine.Contains(c.StokvelId));
        IQueryable<Payout> pq = db.Payouts.AsNoTracking().Where(p => mine.Contains(p.StokvelId));
        IQueryable<StokvelMember> jq = db.StokvelMembers.AsNoTracking().Where(m => mine.Contains(m.StokvelId));

        if (cursorTime is DateTime t)
        {
            var id = cursorId;
            cq = cq.Where(c => c.RecordedAtUtc < t || (c.RecordedAtUtc == t && c.Id.CompareTo(id) < 0));
            pq = pq.Where(p => p.PaidAtUtc < t || (p.PaidAtUtc == t && p.Id.CompareTo(id) < 0));
            jq = jq.Where(m => m.JoinedAtUtc < t || (m.JoinedAtUtc == t && m.UserId.CompareTo(id) < 0));
        }

        var contributions = await cq
            .OrderByDescending(c => c.RecordedAtUtc).ThenByDescending(c => c.Id)
            .Take(take)
            .Select(c => new
            {
                c.Id, c.StokvelId,
                StokvelName = c.Member.Stokvel.Name,
                MemberName = c.Member.User.FullName,
                c.Amount, c.RecordedAtUtc
            })
            .ToListAsync(ct);

        var payouts = await (
                from p in pq
                join s in db.Stokvels on p.StokvelId equals s.Id
                join u in db.Users on p.RecipientUserId equals u.Id
                orderby p.PaidAtUtc descending, p.Id descending
                select new { p.Id, p.StokvelId, StokvelName = s.Name, MemberName = u.FullName, p.Amount, p.PaidAtUtc })
            .Take(take)
            .ToListAsync(ct);

        // a join has no Id of its own, so the user id is its tiebreaker
        var joins = await jq
            .OrderByDescending(m => m.JoinedAtUtc).ThenByDescending(m => m.UserId)
            .Take(take)
            .Select(m => new
            {
                m.UserId, m.StokvelId,
                StokvelName = m.Stokvel.Name,
                MemberName = m.User.FullName,
                m.JoinedAtUtc
            })
            .ToListAsync(ct);

        var merged = contributions
            .Select(c => new Row(c.Id, "Contribution", c.StokvelId, c.StokvelName, c.MemberName,
                Money.ToCents(c.Amount), Utc(c.RecordedAtUtc)))
            .Concat(payouts.Select(p => new Row(p.Id, "Payout", p.StokvelId, p.StokvelName, p.MemberName,
                Money.ToCents(p.Amount), Utc(p.PaidAtUtc))))
            .Concat(joins.Select(m => new Row(m.UserId, "MemberJoined", m.StokvelId, m.StokvelName, m.MemberName,
                null, Utc(m.JoinedAtUtc))))
            // ties are broken like PostgreSQL orders uuids (the plain hex text), so pages never skip a row
            .OrderByDescending(r => r.OccurredAtUtc)
            .ThenByDescending(r => r.Id.ToString("N"), StringComparer.Ordinal)
            .ToList();

        var hasMore = merged.Count > pageSize;
        var page = merged.Take(pageSize).ToList();

        var next = "";
        if (hasMore)
        {
            var last = page[^1];
            next = PageTokenCodec.Encode(new PageCursor(
                context, last.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture), last.Id));
        }

        return new PagedResponse<ActivityItemResponse>(
            page.Select(r => new ActivityItemResponse(
                r.Id, r.Kind, r.StokvelId, r.StokvelName, r.MemberName, r.AmountCents, r.OccurredAtUtc)).ToList(),
            next);
    }
}