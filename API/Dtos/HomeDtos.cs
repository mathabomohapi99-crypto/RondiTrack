namespace RondiTrack.Dtos;

public sealed record HomeResponse(IReadOnlyList<HomeStokvelDto> Stokvels);

public sealed record HomeStokvelDto(
    Guid StokvelId,                  // ⚠ ADAPT: your id type (the screen needs it to navigate)
    string Name,
    StokvelRole Role,                // sent as a name: "Treasurer"
    HomeCycleDto? CurrentCycle,      // null when no cycle is open
    long PaidCents,                  // what I paid this cycle
    long OwedCents,                  // max(0, due - paid)
    DateTimeOffset? NextPayoutAt);   // UTC

public sealed record HomeCycleDto(
    Guid Id,
    CycleStatus Status,              // name, not number
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);