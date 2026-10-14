using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RondiTrack.Auth;
using RondiTrack.Data;
using RondiTrack.Errors;
using RondiTrack.Jobs;                                 // 6.4
using RondiTrack.Services;
using RondiTrack.Telemetry;
using RondiTrack.Validation;
using RondiTrack.Web;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;   // 6.3
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // 6.3: bad JSON becomes the same problem+json shape, with no internal type names
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(
                    kv => JsonNamingPolicy.CamelCase.ConvertName(kv.Key.TrimStart('$', '.')),
                    kv => kv.Value!.Errors
                        .Select(e => e.Exception is null && !string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? e.ErrorMessage : "The value is not valid.")
                        .ToArray());

            var problem = new ProblemDetails { Status = 400, Title = "Validation Failed", Detail = "One or more fields are invalid." };
            problem.Extensions["errors"] = errors;
            problem.Extensions["correlationId"] = CorrelationId.For(ctx.HttpContext);
            return new ObjectResult(problem) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;                  // 6.3
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());               // 6.3: OpenAPI shows enums as names (TypeScript gets "Treasurer", not 1)
});

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["correlationId"] = CorrelationId.For(ctx.HttpContext));

builder.Services.AddExceptionHandler<RondiExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddFrontendCors();        // 6.3
builder.Services.AddRondiRateLimiting();   // 6.3

// ---------- OpenTelemetry (6.2) ----------
var otlpEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
var resource = ResourceBuilder.CreateDefault().AddService(RondiTelemetry.ServiceName);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(RondiTelemetry.ServiceName))
    .WithTracing(t =>
    {
        t.AddSource(RondiTelemetry.ActivitySourceName);   // 6.4: the job spans use this same source
        t.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"));
        t.AddSource("Npgsql");
        if (otlpEnabled) t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddMeter(RondiTelemetry.MeterName);             // 6.4: the cycle/arrears counters use this meter
        m.AddAspNetCoreInstrumentation();
        if (otlpEnabled) m.AddOtlpExporter();
    });

if (otlpEnabled)
{
    builder.Logging.AddOpenTelemetry(o =>
    {
        o.SetResourceBuilder(resource);
        o.IncludeFormattedMessage = true;
        o.AddOtlpExporter();
    });
}

// 6.4: FAIL FAST. A missing setting stops the container at startup with a message that names it.
var connectionString = builder.Configuration.GetConnectionString("RondiTrack")
    ?? throw new InvalidOperationException("Set ConnectionStrings:RondiTrack (user-secrets or ConnectionStrings__RondiTrack).");

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey is missing or shorter than 32 bytes (set Jwt__SigningKey).");
if (jwt.AccessTokenMinutes is < 1 or > 15)
    throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 15.");

builder.Services.AddDbContext<RondiTrackDbContext>(o =>
    o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure(4, TimeSpan.FromSeconds(10), null)));

builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateLifetime = true, RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub", RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy(Policies.StokvelMember, p => p.RequireAuthenticatedUser().AddRequirements(new StokvelAccessRequirement(StokvelAccessLevel.Member)));
    o.AddPolicy(Policies.StokvelOfficer, p => p.RequireAuthenticatedUser().AddRequirements(new StokvelAccessRequirement(StokvelAccessLevel.Officer)));
    o.AddPolicy(Policies.RecordContribution, p => p.RequireAuthenticatedUser().AddRequirements(new StokvelAccessRequirement(StokvelAccessLevel.RecordContribution)));
    o.AddPolicy(Policies.UserSelf, p => p.RequireAuthenticatedUser().AddRequirements(new UserSelfRequirement()));
    o.AddPolicy(Policies.PlatformAdmin, p => p.RequireClaim("role", AuthConstants.PlatformAdminRole));
});

builder.Services.AddScoped<IAuthorizationHandler, StokvelAccessHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, UserSelfHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();
builder.Services.AddScoped<IStokvelAccess, StokvelAccess>();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddSingleton(TimeProvider.System);   // tests swap this for FakeTimeProvider
builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();   // 6.4: was Singleton in-memory. Now in PostgreSQL (Scoped, it uses the DbContext)

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionCycleRepository, EfContributionCycleRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
builder.Services.AddScoped<IStokvelMemberRepository, EfStokvelMemberRepository>();
builder.Services.AddScoped<IContributionQueries, ContributionQueries>();
builder.Services.AddScoped<ContributionPagingService>();
builder.Services.AddScoped<MemberPagingService>();
builder.Services.AddScoped<IStokvelMembershipService, StokvelMembershipService>();
builder.Services.AddScoped<IContributionService, ContributionService>();
builder.Services.AddScoped<IPayoutFaultHook, NoOpPayoutFaultHook>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IHomeService, HomeService>();

// ---------- Background jobs (6.4) ----------
builder.Services.Configure<CycleCloseOptions>(builder.Configuration.GetSection("Jobs:CycleClose"));      // 6.4
builder.Services.Configure<HousekeepingOptions>(builder.Configuration.GetSection("Jobs:Housekeeping"));  // 6.4
builder.Services.AddSingleton<CycleCloseJob>();                                                          // 6.4: also a singleton so tests can call RunOnceAsync
builder.Services.AddHostedService(sp => sp.GetRequiredService<CycleCloseJob>());                         // 6.4
builder.Services.AddSingleton<HousekeepingJob>();                                                        // 6.4
builder.Services.AddHostedService(sp => sp.GetRequiredService<HousekeepingJob>());                       // 6.4

builder.Services.AddHealthChecks()
    .AddDbContextCheck<RondiTrackDbContext>("postgres", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

var readyTimeoutSeconds = builder.Configuration.GetValue("Health:ReadyTimeoutSeconds", 2);
if (readyTimeoutSeconds > 0)
{
    builder.Services.Configure<HealthCheckServiceOptions>(o =>
    {
        foreach (var check in o.Registrations)
            check.Timeout = TimeSpan.FromSeconds(readyTimeoutSeconds);
    });
}

var app = builder.Build();

// 6.4: one-shot migration mode (used by the "migrate" service in compose). Runs and exits; the jobs never start.
if (args.Contains("--migrate"))
{
    using var migrateScope = app.Services.CreateScope();
    await migrateScope.ServiceProvider.GetRequiredService<RondiTrackDbContext>().Database.MigrateAsync();
    Console.WriteLine("Migrations applied.");
    return;
}

if (args.Contains("--seed-volume"))
{
    using var volumeScope = app.Services.CreateScope();
    var volumeDb = volumeScope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    await volumeDb.Database.MigrateAsync();
    await VolumeSeeder.RunAsync(volumeDb);
    return;
}

// 6.4: Demo:Enabled=true (compose sets it) gives Scalar, OpenAPI and demo accounts without running as "Development"
var demoEnabled = app.Configuration.GetValue<bool>("Demo:Enabled");

if (app.Environment.IsDevelopment() || demoEnabled)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    await SeedData.EnsureSeededAsync(db);

    if (demoEnabled)   // 6.4
    {
        var demoPassword = app.Configuration["Demo:Password"];
        if (string.IsNullOrWhiteSpace(demoPassword) || demoPassword.Length < 12)
            throw new InvalidOperationException("Demo:Enabled needs Demo:Password, at least 12 characters (set Demo__Password).");
        await DemoSeed.RunAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>(), demoPassword);
    }
}

// ---------- PIPELINE ORDER (6.3) ----------
app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || demoEnabled)   // 6.4
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(CorsPolicies.Frontend);   // 1. CORS first
app.UseAuthentication();              // 2. who are you?
app.UseRateLimiter();                 // 3. per-user limits need "sub"; before idempotency so a 429 never stores a key
app.UseAuthorization();               // 4. what may you do?
app.MapControllers();                 // 5. idempotency runs inside the contribution service

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

public partial class Program;