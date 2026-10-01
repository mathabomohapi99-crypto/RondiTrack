using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

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

public sealed class PayoutService(RondiTrackDbContext db, IPayoutFaultHook faultHook) : IPayoutService
{
    public async Task<PayoutResponse> ProcessNextPayoutAsync(
        Guid stokvelId, Guid cycleId, CancellationToken ct = default)
    {
        // Retry-on-failure is enabled, so a manual transaction must run inside the execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); // clean slate if the strategy retries

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var cycle = await db.ContributionCycles
                    .FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
                    ?? throw new DomainNotFoundException(
                        $"Cycle {cycleId} was not found for stokvel {stokvelId}.");

                if (cycle.Status != CycleStatus.Open)
                    throw new DomainConflictException("This cycle has already been paid out.");

                var paidMemberIds = db.Payouts
                    .Where(p => p.StokvelId == stokvelId)
                    .Select(p => p.RecipientMemberId);

                var next = await db.StokvelMembers
                    .Where(m => m.StokvelId == stokvelId && !paidMemberIds.Contains(m.Id))
                    .OrderBy(m => m.RotationPosition)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new DomainConflictException("Every member has already been paid in this rotation.");

                var total = await db.Contributions
                    .Where(c => c.CycleId == cycleId)
                    .SumAsync(c => c.Amount, ct);

                if (total <= 0)
                    throw new DomainConflictException("No contributions have been recorded for this cycle.");

                var payout = new Payout(stokvelId, cycleId, next.Id, total);
                db.Payouts.Add(payout);
                await db.SaveChangesAsync(ct);          // write 1: the payout

                faultHook.AfterPayoutInserted();        // a test can throw here

                cycle.MarkPaidOut();
                await db.SaveChangesAsync(ct);          // write 2: the cycle status

                await tx.CommitAsync(ct);

                return new PayoutResponse(
                    payout.Id, payout.StokvelId, payout.CycleId,
                    payout.RecipientMemberId, payout.Amount, payout.PaidAtUtc);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}