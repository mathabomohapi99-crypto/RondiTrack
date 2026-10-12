using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace RondiTrack.Telemetry;

// The ONE correlation id: the W3C trace id of the request
public static class CorrelationId
{
    public static string For(HttpContext context)
    {
        // the request's own activity (carries the caller's traceparent if they sent one)
        var activity = context.Features.Get<IHttpActivityFeature>()?.Activity ?? Activity.Current;

        return activity is { IdFormat: ActivityIdFormat.W3C }
            ? activity.TraceId.ToString()        // 32 hex characters
            : context.TraceIdentifier;           // fallback if there is no activity (e.g. unit tests)
    }
}
