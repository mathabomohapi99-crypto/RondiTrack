namespace RondiTrack.Domain;

// NEW 7.2: where a contribution is in its life.
// Paid = the member says they paid. Pending = recorded, not yet paid. Confirmed = the treasurer checked it.
// Paid is first on purpose: it is the default, and it matches the database default given to old rows.
public enum ContributionStatus
{
    Paid = 0,
    Pending = 1,
    Confirmed = 2
}