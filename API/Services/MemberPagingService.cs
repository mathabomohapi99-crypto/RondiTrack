using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Data;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// NEW: second paged endpoint. Members of a stokvel, in rotation order.
// RotationPosition is unique per stokvel (unique index from 5.2), so it is its own tiebreaker.
public sealed class MemberPagingService(RondiTrackDbContext db)
{
    // Returns null when the stokvel does not exist (controller turns that into 404).
    public async Task<PagedResponse<UserResponse>?> ListAsync(
        Guid stokvelId, int? pageSizeRequested, string? pageToken, CancellationToken ct)
    {
        var pageSize = PagingDefaults.Resolve(pageSizeRequested);

        if (!await db.Stokvels.AnyAsync(s => s.Id == stokvelId, ct)) return null;

        var context = PageTokenCodec.ContextOf("rotation", stokvelId);

        var query = db.StokvelMembers.AsNoTracking().Where(m => m.StokvelId == stokvelId);

        if (!string.IsNullOrEmpty(pageToken))
        {
            var cursor = PageTokenCodec.Decode(pageToken, context);
            if (!int.TryParse(cursor.LastValue, out var lastPosition))
                throw new ApiBadRequestException("pageToken is invalid.");
            query = query.Where(m => m.RotationPosition > lastPosition);   // keyset
        }

        var rows = await query
            .OrderBy(m => m.RotationPosition)
            .Take(pageSize + 1)
            .Select(m => new
            {
                m.RotationPosition,
                m.User.Id,
                m.User.FullName,
                m.User.Email,
                m.User.CreatedAtUtc
            })
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        var next = hasMore
            ? PageTokenCodec.Encode(new PageCursor(context, page[^1].RotationPosition.ToString(), Guid.Empty))
            : "";

        var items = page.Select(r => new UserResponse(r.Id, r.FullName, r.Email, r.CreatedAtUtc)).ToList();
        return new PagedResponse<UserResponse>(items, next);
    }
}