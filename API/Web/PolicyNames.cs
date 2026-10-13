namespace RondiTrack.Web;

public static class CorsPolicies { public const string Frontend = "Frontend"; }

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Contribution = "contribution";
    public const string Payout = "payout";
}

public static class ApiHeaders
{
    public const string IdempotencyKey = "Idempotency-Key";
    public const string IdempotencyReplayed = "Idempotency-Replayed";   // ⚠ ADAPT: use whatever name your replay response already sets
}