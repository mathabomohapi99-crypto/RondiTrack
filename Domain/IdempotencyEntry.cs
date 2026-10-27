namespace RondiTrack.Domain;

// 6.4: idempotency keys now live in PostgreSQL (needed for 2 replicas, and so housekeeping can delete old ones)
public sealed class IdempotencyEntry
{
    public string Key { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public string ResponseJson { get; private set; } = null!;   // the original response, replayed on retry
    public DateTime CreatedAtUtc { get; private set; }

    private IdempotencyEntry() { }   // for EF only

    public IdempotencyEntry(string key, string requestHash, string responseJson, DateTime createdAtUtc)
    {
        Key = key; RequestHash = requestHash; ResponseJson = responseJson; CreatedAtUtc = createdAtUtc;
    }
}