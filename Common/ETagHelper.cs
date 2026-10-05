using Microsoft.AspNetCore.Http;
using RondiTrack.Api.Common.Paging;

namespace RondiTrack.Common;

// NEW: turns the xmin version number into an ETag header and back.
public static class ETagHelper
{
    // Puts the entity's version on the response as: ETag: "12345"
    public static void Set(HttpResponse response, uint version) =>
        response.Headers.ETag = $"\"{version}\"";

    // Reads the If-Match header. Missing -> 428, not a number -> 400.
    public static uint ParseIfMatch(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            throw new PreconditionRequiredException(
                "The If-Match header is required. Send the ETag you received from GET.");

        var text = header.Trim();
        if (text.StartsWith("W/")) text = text[2..];
        text = text.Trim('"');

        if (!uint.TryParse(text, out var version))
            throw new ApiBadRequestException("The If-Match header is not a valid ETag.");

        return version;
    }
}