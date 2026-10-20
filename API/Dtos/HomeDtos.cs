namespace RondiTrack.Dtos;

public sealed record HomeResponse(IReadOnlyList<HomeStokvelDto> Stokvels);

public sealed record HomeStokvelDto(
    Guid StokvelId,
    string Name,
    StokvelRole Role,                // sent as a name: "Treasurer"
    HomeCycleDto? CurrentCycle,      // null when no cycle is open
    long PaidCents,                  // what I paid this cycle
    long OwedCents,                  // max(0, due - paid)
    DateTime? NextPayoutAt,          // CHANGED 7.2: DateTime (UTC) so it always ends in Z
    long? ArrearsCents);             // NEW 7.2: MY OWN arrears in this stokvel, null when none. Never another member's.

public sealed record HomeCycleDto(
    Guid Id,
    int Number,                      // NEW 7.2: the cycle number
    CycleStatus Status,              // name, not number
    DateTime StartsAt,               // CHANGED 7.2: DateTime (UTC), ends in Z
    DateTime EndsAt);                // the due date