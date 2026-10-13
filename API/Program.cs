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
// using RondiTrack.Web;   // TODO 6.3: uncomment when CorsSetup / RateLimitingSetup exist

// =====================================================================
// PART 1: SERVICES (everything registered here is available via DI)
// =====================================================================

var builder = WebApplication.CreateBuilder(args);

// Turn on controllers. ValidationFilter runs FluentValidation on every request.
// JsonStringEnumConverter sends enums as names ("Treasurer") instead of numbers.
builder.Services
    .AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// TODO 6.3 (Task 5): also set NumberHandling = JsonNumberHandling.Strict in the lambda above,
// and add builder.Services.ConfigureHttpJsonOptions(...) so OpenAPI shows plain numbers.

// Generates the OpenAPI