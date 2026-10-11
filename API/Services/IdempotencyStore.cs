using System.Collections.Concurrent;
using RondiTrack.Dtos;

namespace RondiTrack.Data;

public sealed record IdempotencyRecord(string RequestHash, ContributionResponse Response);

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key);
    Task SaveAsync(string key, IdempotencyRecord record);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new();

    public Task<IdempotencyRecord?> GetAsync(string key)
    {
        _records.TryGetValue(key, out var record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(string key, IdempotencyRecord record)
    {
        _records[key] = record;
        return Task.CompletedTask;
    }
}