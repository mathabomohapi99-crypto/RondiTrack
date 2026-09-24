using System.Text.Json.Serialization;
using FluentValidation;
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

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IStokvelRepository, InMemoryStokvelRepository>();
builder.Services.AddSingleton<IContributionRepository, InMemoryContributionRepository>();
builder.Services.AddSingleton<IContributionCycleRepository, InMemoryContributionCycleRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

builder.Services.AddScoped<IStokvelMembershipService, StokvelMembershipService>();
builder.Services.AddScoped<IContributionService, ContributionService>();

var app = builder.Build();

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