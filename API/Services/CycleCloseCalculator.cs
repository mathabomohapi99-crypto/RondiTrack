using System.Security.Cryptography;
using System.Text;

namespace RondiTrack.Services;

public sealed record ArrearLine(Guid UserId, long ShortfallCents);

// NEW 9.1: THE one place that decides who goes into arrears. The nightly job and the close preview both call it,
// so the dialog can never say something different from what the close does.
public static class CycleCloseCalculator
{
    public static List<ArrearLine> Arrears(long dueCents, IEnumerable<Guid> memberIds, IReadOnlyDictionary<Guid, long> paidByUser)
    {
        var lines = new List<ArrearLine>();
        foreach (var id in memberIds)
        {
            paidByUser.TryGetValue(id, out var paid);
            var shortfall = dueCents - paid;
            if (shortfall > 0) lines.Add(new ArrearLine(id, shortfall));   // paid in full = no arrear
        }
        return lines;
    }

    // A short fingerprint of "who paid what" in this cycle. Any new contribution or member change gives a new one.
    public static string Version(Guid cycleId, long dueCents, IEnumerable<Guid> memberIds, IReadOnlyDictionary<Guid, long> paidByUser)
    {
        var parts = memberIds.OrderBy(id => id)
            .Select(id => $"{id:N}:{(paidByUser.TryGetValue(id, out var p) ? p : 0)}");
        var text = $"{cycleId:N}|{dueCents}|{string.Join(",", parts)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    // South Africa is UTC+2 all year, so the SA date is the UTC time plus 2 hours
    public static DateOnly SaToday(DateTimeOffset nowUtc) =>
        DateOnly.FromDateTime(nowUtc.ToOffset(TimeSpan.FromHours(2)).DateTime);
}