using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Telemetry;

namespace RondiTrack.Web;

public static class RateLimitingSetup
{
    public static IServiceCollection AddRondiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((o, cfg) =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;   // default is 503
            o.OnRejected = WriteRejectionAsync;

            var login = cfg.GetSection("RateLimiting:Login");
            o.AddPolicy(RateLimitPolicies.Login, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = login.GetValue("PermitLimit", 5),
                    Window = TimeSpan.FromSeconds(login.GetValue("WindowSeconds", 60.0)),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            o.AddPolicy(RateLimitPolicies.Contribution, ctx =>
                RateLimitPartition.GetTokenBucketLimiter(UserKey(ctx), _ => Bucket(cfg.GetSection("RateLimiting:Contribution"))));

            o.AddPolicy(RateLimitPolicies.Payout, ctx =>
                RateLimitPartition.GetTokenBucketLimiter(UserKey(ctx), _ => Bucket(cfg.GetSection("RateLimiting:Payout"))));
        });
        return services;
    }

    private static TokenBucketRateLimiterOptions Bucket(IConfigurationSection s) => new()
    {
        TokenLimit = s.GetValue("TokenLimit", 30),
        TokensPerPeriod = s.GetValue("TokensPerPeriod", 5),
        ReplenishmentPeriod = TimeSpan.FromSeconds(s.GetValue("ReplenishSeconds", 60.0)),
        QueueLimit = 0,              // reject, never hold a money request open
        AutoReplenishment = true
    };

    private static string ClientIp(HttpContext c) => "ip:" + (c.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    // signed-in user id; falls back to IP only for unauthenticated callers (they'll get 401 afterwards)
    private static string UserKey(HttpContext c) =>
        c.User.FindFirstValue("sub") is { Length: > 0 } sub ? "user:" + sub : ClientIp(c);

    private static async ValueTask WriteRejectionAsync(OnRejectedContext ctx, CancellationToken ct)
    {
        var http = ctx.HttpContext;
        var seconds = 1;
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            seconds = Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds));

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = seconds.ToString();

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 429,
            Title = "Too many requests",
            Detail = $"Rate limit reached. Try again in {seconds} second(s).",
            Type = "https://www.rfc-editor.org/rfc/rfc6585#section-4"
        };
        problem.Extensions["correlationId"] = CorrelationId.For(http);   // your 6.2 helper
        await http.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", ct);
    }
}