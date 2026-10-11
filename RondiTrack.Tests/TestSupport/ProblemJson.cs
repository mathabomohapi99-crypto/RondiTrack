namespace RondiTrack.Tests.TestSupport;

using System.Text.Json;

// NEW 6.1: reads a problem response so two responses can be compared
public static class ProblemJson
{
    public static async Task<(int Status, string? Title, string? Detail)> ReadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return (
            root.GetProperty("status").GetInt32(),
            root.TryGetProperty("title", out var title) ? title.GetString() : null,
            root.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
    }
}