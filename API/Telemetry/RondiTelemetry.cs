using System.Diagnostics;

namespace RondiTrack.Telemetry;

// One place for the names, so Program.cs and the code that emits agree
public static class RondiTelemetry
{
    public const string ServiceName = "ronditrack-api";      // service name shown in the dashboard
    public const string ActivitySourceName = "RondiTrack";   // our own span source (registered in Program.cs)
    public const string MeterName = "RondiTrack";            // our own meter (registered in Program.cs)

    public static readonly ActivitySource Source = new(ActivitySourceName);   // creates our business spans
}