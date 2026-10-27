namespace RondiTrack.Common;

// NEW 7.2: money travels through the API as integer cents. The rounding lives in ONE place.
public static class Money
{
    public static long ToCents(decimal amount) => (long)Math.Round(amount * 100m);
}