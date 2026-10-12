using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;   // NEW 6.2: HealthCheckOptions
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;   // NEW 6.2: HealthStatus, HealthCheckServiceOptions
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;                              // NEW 6.2: OTLP exporter for logs
using OpenTelemetry.Metrics;                           // NEW 6.2: AddMeter + OTLP exporter for metrics
using OpenTelemetry.Resources;                         // NEW 6.2: AddService (service name)
using OpenTelemetry.Trace;                             // NEW 6.2: AddSource, AddNpgsql, OTLP exporter for traces
using RondiTrack.Auth;
using RondiTrack.Data;
using RondiTrack.Errors;
using RondiTrack.Services;
using RondiTrack.Telemetry;                            // NEW 6.2
using RondiTrack.Validation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// CHANGED 6.2: framework-made problem responses (e.g. automatic 400s) also get the trace id as correlationId
builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["correlationId"] = CorrelationId.For(ctx.HttpContext));

builder.Services.AddExceptionHandler<RondiExceptionHandler>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------- NEW 6.2: OpenTelemetry ----------
// The exporter is only added when OTEL_EXPORTER_OTLP_ENDPOINT is set, so tests and CI never need a dashboard
var otlpEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
var resource = ResourceBuilder.CreateDefault().AddService(RondiTelemetry.ServiceName);   // service name for all signals

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(RondiTelemetry.ServiceName))
    .WithTracing(t =>
    {
        t.AddSource(RondiTelemetry.ActivitySourceName);   // our payout.process span
        t.AddAspNetCoreInstrumentation(o =>
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"));   // probes are not traced
        t.AddNpgsql();                                     // one span per SQL command
        if (otlpEnabled) t.AddOtlpExporter();              // send traces only if an endpoint is set
    })
    .WithMetrics(m =>
    {
        m.AddMeter(RondiTelemetry.MeterName);              // our business counters
        m.AddAspNetCoreInstrumentation();                  // request count and duration
        if (otlpEnabled) m.AddOtlpExporter();              // send metrics only if an endpoint is set
    });

if (otlpEnabled)
{
    builder.Logging.AddOpenTelemetry(o =>
    {
        o.SetResourceBuilder(resource);                    // same service name on logs
        o.IncludeFormattedMessage = true;                  // readable message in the dashboard
        o.AddOtlpExporter();                               // send logs only if an endpoint is set
    });
}

var connectionString = builder.Configuration.GetConnectionString("RondiTrack")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings:RondiTrack with dotnet user-secrets.");

// NEW 6.1: read the JWT settings. The signing key must come from user-secrets and be at least 32 bytes.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException(
        "Jwt:SigningKey is missing or shorter than 32 bytes. From the API folder run: " +
        "$key = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48)); " +
        "dotnet user-secrets set \"Jwt:SigningKey\" $key");
if (jwt.AccessTokenMinutes is < 1 or > 15)
    throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 15.");   // keeps tokens short-lived

builder.Services.AddDbContext<RondiTrackDbContext>(o =>
{
    o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure(
        maxRetryCount: 4,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorCodesToAdd: null));

    // 6.2: no LogTo and no sensitive-data logging anywhere. SQL is logged once by the
    // "Microsoft.EntityFrameworkCore.Database.Command" category, whose level is set per environment in appsettings.
});

// NEW 6.1: authentication (who are you?)
builder.Services.AddSingleton(jwt);   // settings available to TokenService and AuthService
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // keep claim names as "sub" and "role" (no renaming)
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],   // only the algorithm we sign with
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),   // default is 5 MINUTES, which would stretch a 10 min token to 15
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

// NEW 6.1: authorization (what may you do?)
builder.Services.AddAuthorization(o =>
{
    // secure by default: any endpoint without [Authorize] or [AllowAnonymous] needs a signed-in user
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // stokvel policies: the rule itself lives in StokvelAccessHandler
    o.AddPolicy(Policies.StokvelMember, p => p.RequireAuthenticatedUser()
        .AddRequirements(new StokvelAccessRequirement(StokvelAccessLevel.Member)));
    o.AddPolicy(Policies.StokvelOfficer, p => p.RequireAuthenticatedUser()
        .AddRequirements(new StokvelAccessRequirement(StokvelAccessLevel.Officer)));
    o.AddPolicy(Policies.RecordContribution, p => p.RequireAuthenticatedUser()
        .AddRequirements(new StokvelAccessRequirement(StokvelAccessLevel.RecordContribution)));

    o.AddPolicy(Policies.UserSelf, p => p.RequireAuthenticatedUser()
        .AddRequirements(new UserSelfRequirement()));
    o.AddPolicy(Policies.PlatformAdmin, p => p.RequireClaim("role", AuthConstants.PlatformAdminRole));
});

builder.Services.AddScoped<IAuthorizationHandler, StokvelAccessHandler>();   // Scoped: it reads the database
builder.Services.AddSingleton<IAuthorizationHandler, UserSelfHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();   // 401/403 as problem+json
builder.Services.AddScoped<IStokvelAccess, StokvelAccess>();

// NEW 6.1: password hashing (ships with ASP.NET Core, no Identity stack) and token services
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();   // PostgreSQL, not memory
builder.Services.AddScoped<IAuthService, AuthService>();

// Still in-memory on purpose (not part of today's work)
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

// Everything below is EF Core now. Scoped, because a DbContext lives for one request
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionCycleRepository, EfContributionCycleRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
builder.Services.AddScoped<IStokvelMemberRepository, EfStokvelMemberRepository>();   // EDIT 5.2: new
builder.Services.AddScoped<IContributionQueries, ContributionQueries>();               // EDIT 5.2: new

// ADDED 5.3: the two paging services
builder.Services.AddScoped<ContributionPagingService>();
builder.Services.AddScoped<MemberPagingService>();

builder.Services.AddScoped<IStokvelMembershipService, StokvelMembershipService>();
builder.Services.AddScoped<IContributionService, ContributionService>();

builder.Services.AddScoped<IPayoutFaultHook, NoOpPayoutFaultHook>();
builder.Services.AddScoped<IPayoutService, PayoutService>();

// ---------- NEW 6.2: health checks ----------
// Only the "ready" check touches PostgreSQL. /health/live runs nothing.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RondiTrackDbContext>("postgres", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

// Cap on how long a readiness check may run (seconds). 0 = no cap (only used to measure "before" in the README).
var readyTimeoutSeconds = builder.Configuration.GetValue("Health:ReadyTimeoutSeconds", 2);
if (readyTimeoutSeconds > 0)
{
    builder.Services.Configure<HealthCheckServiceOptions>(o =>
    {
        foreach (var check in o.Registrations)
            check.Timeout = TimeSpan.FromSeconds(readyTimeoutSeconds);   // a hung database cannot hang the probe
    });
}

var app = builder.Build();

// ADDED 5.3: volume seeding only runs when you ask for it:  dotnet run -- --seed-volume
// It never runs on normal startup. It applies migrations first, seeds, then stops (no web server).
if (args.Contains("--seed-volume"))
{
    using var volumeScope = app.Services.CreateScope();
    var volumeDb = volumeScope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    await volumeDb.Database.MigrateAsync();
    await VolumeSeeder.RunAsync(volumeDb);
    return;
}

// Put the demo data into Postgres (Development only, needs the migration applied first)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    await SeedData.EnsureSeededAsync(db);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();               // CHANGED 6.1: docs load without a token
    app.MapScalarApiReference().AllowAnonymous();    // CHANGED 6.1
}

app.UseHttpsRedirection();
app.UseAuthentication();   // NEW 6.1: reads the token (who are you?)
app.UseAuthorization();    // NEW 6.1: applies the policies (what may you do?)
app.MapControllers();

// NEW 6.2: probes. AllowAnonymous beats the 6.1 fallback policy. Output is only "Healthy" or "Unhealthy".
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })   // no checks at all
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") })   // PostgreSQL only
    .AllowAnonymous();

app.Run();

public partial class Program;