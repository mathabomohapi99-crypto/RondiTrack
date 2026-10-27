using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RondiTrack.Tests.TestSupport;

// One captured log line: the text, the exception text and the structured values
public sealed record CapturedLog(string Category, LogLevel Level, string Message, string? Exception, string Properties)
{
    public string AllText => $"{Message} {Exception} {Properties}";   // everything we search for leaks
}

// Test-only logger provider that remembers every log line in memory
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();   // safe when many requests log at once

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();   // a snapshot copy

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;   // capture every level

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? string.Join(";", values.Select(v => $"{v.Key}={v.Value}"))   // structured values too
                : "";
            sink.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception?.ToString(), properties));
        }
    }
}