using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RondiTrack.Api.Common.Paging;

// CHANGED: LastId is now a Guid (your ids are Guids, not ints).
// Context = fingerprint of the sort + filters, so we can detect a token reused with different ones.
public record PageCursor(string Context, string LastValue, Guid LastId);

public static class PageTokenCodec
{
    // Turns the cursor into an opaque base64url string the client cannot read.
    public static string Encode(PageCursor cursor)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(json).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    // Reads a token back. A broken token, or a token from a different sort/filter, gives a 400.
    public static PageCursor Decode(string token, string expectedContext)
    {
        PageCursor? cursor;
        try
        {
            var b64 = token.Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            cursor = JsonSerializer.Deserialize<PageCursor>(Convert.FromBase64String(b64));
        }
        catch
        {
            throw new ApiBadRequestException("pageToken is invalid.");
        }

        if (cursor is null)
            throw new ApiBadRequestException("pageToken is invalid.");

        if (cursor.Context != expectedContext)
            throw new ApiBadRequestException(
                "pageToken was issued for a different sort or filter. Start again without a pageToken.");

        return cursor;
    }

    // Short fingerprint of sort + filters (same inputs always give the same text).
    public static string ContextOf(params object?[] parts)
    {
        var text = string.Join("|", parts.Select(p => p?.ToString() ?? ""));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
    }
}