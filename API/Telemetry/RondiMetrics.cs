using System.Diagnostics.Metrics;

namespace RondiTrack.Telemetry;

// The ONLY allowed tag values. Fixed lists keep every metric small (see README table).
public static class PayoutOutcomes
{
    public const string Success = "success";
    public const string Rejected = "rejected";   // a business rule said no
    public const string Error = "error";         // something broke (database, bug)
}

public static class RefusalResults
{
    public const string NotFound = "not_found";   // non-member got the 404
    public const string Forbidden = "forbidden";  // member (or user) lacked permission
}

// Static on purpose: services stay constructible by hand in unit tests
public static class RondiMetrics
{
    public const string ContributionsRecordedName = "ronditrack.contributions.recorded";
    public const string PayoutsProcessedName = "ronditrack.payouts.processed";
    public const string AuthorizationRefusalsName = "ronditrack.authorization.refusals";

    private static readonly Meter AppMeter = new(RondiTelemetry.MeterName);

    private static readonly Counter<long> Contributions =
        AppMeter.CreateCounter<long>(ContributionsRecordedName, description: "Contributions recorded");   // no tags

    private static readonly Counter<long> Payouts =
        AppMeter.CreateCounter<long>(PayoutsProcessedName, description: "Payouts processed, by outcome");

    private static readonly Counter<long> Refusals =
        AppMeter.CreateCounter<long>(AuthorizationRefusalsName, description: "Authorization refusals, by result");

    public static void ContributionRecorded() => Contributions.Add(1);

    // tag "outcome": success | rejected | error  (3 values)
    public static void PayoutProcessed(string outcome) =>
        Payouts.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    // tag "result": not_found | forbidden  (2 values)
    public static void AuthorizationRefused(string result) =>
        Refusals.Add(1, new KeyValuePair<string, object?>("result", result));
}