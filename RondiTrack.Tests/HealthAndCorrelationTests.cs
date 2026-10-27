using System.Net;
using System.Text.Json;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 6.2: probes work without a token, and every problem response carries the caller's trace id
[Collection("Postgres collection")]
public class HealthAndCorrelationTests(PostgresApiFactory factory)
{
    private const string SentTraceId = "0af7651916cd43dd8448eb211c80319c";          // the trace id we send
    private const string Traceparent = $"00-{SentTraceId}-b7ad6b7169203331-01";   // W3C traceparent header value

    // ---- probes ----

    [Fact]
    public async Task Liveness_Returns_200_Without_A_Token()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/health/live");   // no token on purpose

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());   // only the status word
    }

    [Fact]
    public async Task Readiness_Returns_200_Without_A_Token()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    // ---- correlation id ----

    private static HttpRequestMessage WithTraceparent(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("traceparent", Traceparent);   // pretend the caller already has a trace
        return request;
    }

    private static async Task<string?> CorrelationIdAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("correlationId").GetString();   // read it from the problem json
    }

    [Fact]
    public async Task Anonymous_401_Returns_The_Sent_Trace_Id_As_CorrelationId()
    {
        var response = await factory.CreateAnonymousClient()
            .SendAsync(WithTraceparent($"/api/v1/stokvels/{Guid.NewGuid()}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(SentTraceId, await CorrelationIdAsync(response));   // same id we sent
    }

    [Fact]
    public async Task Non_Member_404_Returns_The_Sent_Trace_Id_As_CorrelationId()
    {
        Guid stokvelId;
        await using (var db = TestDb.NewDb())
        {
            var stokvel = new Stokvel($"Corr {Guid.NewGuid()}", 100m, ContributionFrequency.Monthly, 5);
            db.Stokvels.Add(stokvel);   // a real stokvel with NO members
            await db.SaveChangesAsync();
            stokvelId = stokvel.Id;
        }

        var outsider = factory.CreateClientFor(Guid.NewGuid());   // signed in, but not a member
        var response = await outsider.SendAsync(WithTraceparent($"/api/v1/stokvels/{stokvelId}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(SentTraceId, await CorrelationIdAsync(response));
    }

    [Fact]
    public async Task Framework_403_Returns_The_Sent_Trace_Id_As_CorrelationId()
    {
        var user = factory.CreateClientFor(Guid.NewGuid());   // not a platform admin
        var response = await user.SendAsync(WithTraceparent("/api/v1/stokvels"));   // platform admins only

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(SentTraceId, await CorrelationIdAsync(response));
    }

    [Fact]
    public async Task Without_A_Traceparent_The_CorrelationId_Is_A_New_32_Character_Trace_Id()
    {
        var response = await factory.CreateAnonymousClient().GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Matches("^[0-9a-f]{32}$", await CorrelationIdAsync(response));   // 32 hex characters
    }
}