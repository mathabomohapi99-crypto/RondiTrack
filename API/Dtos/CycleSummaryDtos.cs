using RondiTrack.Domain;

namespace RondiTrack.Dtos;

// NEW 7.2: one row of GET /stokvels/{id}/cycles. Totals are worked out HERE so the browser never adds anything up.
public sealed record CycleSummaryResponse(
    Guid Id,
    int Number,
    CycleStatus Status,       // Open | Closed | PaidOut
    DateTime StartsAt,        // UTC instant (ends in Z)
    DateTime EndsAt,          // the due date, UTC instant (ends in Z)
    long CollectedCents,      // everything collected in this cycle
    long DueCents);           // everything due this cycle (contribution x members)