using Microsoft.AspNetCore.Cors.Infrastructure;

namespace RondiTrack.Web;

public static class CorsSetup
{
    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors();
        // Options pattern = read lazily, so test UseSetting overrides are always seen
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((o, config) =>
        {
            var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Any(x => x.Contains('*') || x.EndsWith('/')))
                throw new InvalidOperationException("Cors:AllowedOrigins must be exact origins: no '*', no trailing slash.");

            o.AddPolicy(CorsPolicies.Frontend, p => p
                .WithOrigins(origins)                                   // empty list = nobody allowed
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                .WithHeaders("Content-Type", "Authorization", ApiHeaders.IdempotencyKey, "If-Match", "If-None-Match")
                .WithExposedHeaders("Retry-After", ApiHeaders.IdempotencyReplayed, "ETag", "Location")
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
        });
        return services;
    }

    /// The exception handler clears response headers, so CORS headers set earlier are lost on
    /// exception-based errors (404/403/409/500). Call this at the top of the handler to put them back.
    public static async Task ReapplyAsync(HttpContext ctx)
    {
        var provider = ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>();
        var service = ctx.RequestServices.GetRequiredService<ICorsService>();
        var policy = await provider.GetPolicyAsync(ctx, CorsPolicies.Frontend);
        if (policy is null) return;
        service.ApplyResult(service.EvaluatePolicy(ctx, policy), ctx.Response);
    }
}