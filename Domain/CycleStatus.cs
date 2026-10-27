namespace RondiTrack.Domain;

public enum CycleStatus
{
    Open = 0,
    PaidOut = 1,
    Closed = 2   // CHANGED 7.2: never stored. Worked out from ClosedAtUtc (set by the cycle-close job)
}