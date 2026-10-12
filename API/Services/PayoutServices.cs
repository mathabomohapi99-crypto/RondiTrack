using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Telemetry;

namespace RondiTrack.Services;

// Lets a test force a failure between the two writes. Does nothing in production.
public interface IPayoutFaultHook
{
    void AfterPayoutInserted();
}

public sealed class NoOpPayoutFaultHook : IPayoutFaultHook
{
    public void AfterPayoutInserted() { }
}

public interface IPayoutService
{
    Task<PayoutResponse> ProcessNextPayoutAsync(Guid stokvelId, Guid cycleId, CancellationToken ct = default);
}

// EDIT 5.2: now also receives IStokvelMemberRepository (members are no longer read straight from the DbContext)
public sealed class PayoutService(
    RondiTrackDbContext db,
    IPayoutFaultHook faultHook,
    IStokvelMemberRepository members) : IPayoutService
{
    // NEW 6.2: the public method only adds the span and the metric. The money logic is unchanged (below).
    public async Task<PayoutResponse> ProcessNextPayoutAsync(
        Guid stokvelId, Guid cycleId, CancellationToken ct = default)
    {
        // our own span, named for the business operation. The SQL spans start INSIDE it, so they nest under it.
        using var activity = RondiTelemetry.Source.StartActivity("payout.process");

        try
        {
            var result = await ProcessAsync(stokvelId, cycleId, activity, ct);

            activity?.SetTag("payout.outcome", PayoutOutcomes.Success);   // safe: fixed label
            RondiMetrics.PayoutProcessed(PayoutOutcomes.Success);         // counter, tagged by outcome
            return result;
        }
        catch (Exception ex)
        {
            // a rule said no = "rejected"; anything else (database, bug) = "error"
            var outcome = ex is DomainException ? PayoutOutcomes.Rejected : PayoutOutcomes.Error;

            activity?.SetTag("payout.outcome", outcome);
            activity?.SetTag("error.type", ex.GetType().Name);            // class name only, never the message
            activity?.SetStatus(ActivityStatusCode.Error);                // a failed payout = error span (no description on purpose)
            RondiMetrics.PayoutProcessed(outcome);
            throw;                                                        // the exception handler still turns it into a response
        }
    }

    // The original 5.x logic, moved here unchanged except for the one cycle-number tag
    private async Task<PayoutResponse> ProcessAsync(
        Guid stokvelId, Guid cycleId, Activity? activity, CancellationToken ct)
    {
        // Retry-on-failure is enabled, so a manual transaction must run inside the execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); // clean slate if the strategy retries

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                // Stays TRACKED on purpose, because we change its status and save below
                var cycle = await db.ContributionCycles
                    .FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
                    ?? throw new DomainNotFoundException(
                        $"Cycle {cycleId} was not found for stokvel {stokvelId}.");

                activity?.SetTag("payout.cycle_number", cycle.CycleNumber);   // safe: a sequence number (1, 2, 3...)

                if (cycle.Status != CycleStatus.Open)
                    throw new DomainConflictException("This cycle has already been paid out.");

                // Payouts now record the recipient's UserId (not a StokvelMember.Id)
                var paidUserIds = await db.Payouts
                    .Where(p => p.StokvelId == stokvelId)
                    .Select(p => p.RecipientUserId)
                    .ToListAsync(ct);

                // EDIT 5.2: read-only (untracked) through the dedicated repository, already ordered by rotation
                var next = (await members.GetByStokvelAsync(stokvelId))
                    .FirstOrDefault(m => !paidUserIds.Contains(m.UserId))
                    ?? throw new DomainConflictException("Every member has already been paid in this rotation.");

                var total = await db.Contributions
                    .Where(c => c.CycleId == cycleId)
                    .SumAsync(c => c.Amount, ct);

                if (total <= 0)
                    throw new DomainConflictException("No contributions have been recorded for this cycle.");

                var payout = new Payout(stokvelId, cycleId, next.UserId, total);   // EDIT 5.2: next.UserId
                db.Payouts.Add(payout);
                await db.SaveChangesAsync(ct);          // write 1: the payout

                faultHook.AfterPayoutInserted();        // a test can throw here

                cycle.MarkPaidOut();
                await db.SaveChangesAsync(ct);          // write 2: the cycle status

                await tx.CommitAsync(ct);

                return new PayoutResponse(
                    payout.Id, payout.StokvelId, payout.CycleId,
                    payout.RecipientUserId, payout.Amount, payout.PaidAtUtc);   // EDIT 5.2
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}