using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Errors;
using RondiTrack.Services;
using RondiTrack.Validation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RondiExceptionHandler>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var connectionString = builder.Configuration.GetConnectionString("RondiTrack")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings:RondiTrack with dotnet user-secrets.");

builder.Services.AddDbContext<RondiTrackDbContext>(o =>
{
    o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure(
        maxRetryCount: 4,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorCodesToAdd: null));

    // ADDED 5.3: Development only. Shows real parameter values in the SQL log,
    // so the logged SQL can be pasted straight into EXPLAIN ANALYZE.
    o.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

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
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;