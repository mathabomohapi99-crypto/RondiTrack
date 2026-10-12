using System.Diagnostics;
using System.Diagnostics.Metrics;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;
using RondiTrack.Telemetry;
using Xunit;

namespace RondiTrack.Tests;

// NEW 6.2: the payout metric and the payout.process span
[Collection("Postgres collection")]   // starts the Testcontainers database first (TestDb needs it)
public class TelemetryTests
{
    // throws between the two writes, to force a failed payout
    private sealed class ThrowingHook : IPayoutFaultHook
    {
        public void AfterPayoutInserted() => throw new InvalidOperationException("Simulated failure");
    }

    // a stokvel with one member, one cycle and one contribution, so a payout can succeed
    private static async Task<(Guid StokvelId, Guid CycleId)> SeedPayableCycleAsync()
    {
        var (stokvelId, userId, cycleId) = await TestDb.SeedMemberAndCycleAsync();
        await using var db = TestDb.NewDb();
        db.Contributions.Add(new Contribution(stokvelId, userId, cycleId, 100m));
        await db.SaveChangesAsync();
        return (stokvelId, cycleId);
    }

    // builds the real PayoutService and runs one payout
    private static Task<PayoutResponse> PayAsync(Guid stokvelId, Guid cycleId, IPayoutFaultHook? hook = null)
    {
        var db = TestDb.NewDb();
        return new PayoutService(db, hook ?? new NoOpPayoutFaultHook(), new EfStokvelMemberRepository(db))
            .ProcessNextPayoutAsync(stokvelId, cycleId);
    }

    // built-in MeterListener: records the "outcome" tag of every payouts measurement
    private static MeterListener ListenToPayouts(List<string?> outcomes)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == RondiTelemetry.MeterName && instrument.Name == RondiMetrics.PayoutsProcessedName)
                l.EnableMeasurementEvents(instrument);   // only listen to our payouts counter
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            string? outcome = null;
            foreach (var tag in tags)
                if (tag.Key == "outcome") outcome = tag.Value?.ToString();
            lock (outcomes) outcomes.Add(outcome);
        });
        listener.Start();
        return listener;
    }

    [Fact]
    public async Task A_Successful_Payout_Increments_The_Payouts_Counter_With_Outcome_Success()
    {
        var (stokvelId, cycleId) = await SeedPayableCycleAsync();
        var outcomes = new List<string?>();
        using var listener = ListenToPayouts(outcomes);

        await PayAsync(stokvelId, cycleId);

        lock (outcomes) Assert.Contains("success", outcomes);
    }

    [Fact]
    public async Task A_Refused_Payout_Is_Counted_As_Rejected_And_A_Broken_One_As_Error()
    {
        var (stokvelId, cycleId) = await SeedPayableCycleAsync();
        var outcomes = new List<string?>();
        using var listener = ListenToPayouts(outcomes);

        await PayAsync(stokvelId, cycleId);                                                       // first payout works
        await Assert.ThrowsAsync<DomainConflictException>(() => PayAsync(stokvelId, cycleId));   // already paid = rejected

        var (stokvel2, cycle2) = await SeedPayableCycleAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PayAsync(stokvel2, cycle2, new ThrowingHook()));                                // a bug mid-transaction = error

        lock (outcomes)
        {
            Assert.Contains("rejected", outcomes);
            Assert.Contains("error", outcomes);
        }
    }

    // records finished payout.process spans
    private static ActivityListener ListenToSpans(List<Activity> spans)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RondiTelemetry.ActivitySourceName,   // only our source
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (spans) spans.Add(activity); }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task Payout_Span_Carries_Safe_Tags_And_No_Error_Status_On_Success()
    {
        var (stokvelId, cycleId) = await SeedPayableCycleAsync();
        var spans = new List<Activity>();
        using var listener = ListenToSpans(spans);

        await PayAsync(stokvelId, cycleId);

        Activity span;
        lock (spans) span = Assert.Single(spans, a => a.OperationName == "payout.process");
        Assert.Equal("success", span.GetTagItem("payout.outcome")?.ToString());
        Assert.Equal("1", span.GetTagItem("payout.cycle_number")?.ToString());
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);

        // the payout was 100: no tag value may contain the amount
        Assert.All(span.TagObjects, t => Assert.DoesNotContain("100", t.Value?.ToString() ?? ""));
    }

    [Fact]
    public async Task A_Failed_Payout_Sets_The_Span_Status_To_Error()
    {
        var (stokvelId, cycleId) = await SeedPayableCycleAsync();
        var spans = new List<Activity>();
        using var listener = ListenToSpans(spans);

        await Assert.ThrowsAsync<InvalidOperationException>(() => PayAsync(stokvelId, cycleId, new ThrowingHook()));

        Activity span;
        lock (spans) span = Assert.Single(spans, a => a.OperationName == "payout.process");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("error", span.GetTagItem("payout.outcome")?.ToString());
        Assert.Equal("InvalidOperationException", span.GetTagItem("error.type")?.ToString());
    }
}