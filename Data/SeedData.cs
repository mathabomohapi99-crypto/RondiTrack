using RondiTrack.Domain;

namespace RondiTrack.Data;

internal static class SeedData
{
    public static IReadOnlyList<User> Users { get; }
    public static IReadOnlyList<Stokvel> Stokvels { get; }

    static SeedData()
    {
        var thandi  = new User(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Thandi Nkosi",   "thandi.nkosi@example.com");
        var sipho   = new User(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Sipho Dlamini",  "sipho.dlamini@example.com");
        var lerato  = new User(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Lerato Mokoena", "lerato.mokoena@example.com");
        var kagiso  = new User(Guid.Parse("10000000-0000-0000-0000-000000000004"), "Kagiso Molefe",  "kagiso.molefe@example.com");
        var naledi  = new User(Guid.Parse("10000000-0000-0000-0000-000000000005"), "Naledi Khumalo", "naledi.khumalo@example.com");
        var bongani = new User(Guid.Parse("10000000-0000-0000-0000-000000000006"), "Bongani Zulu",   "bongani.zulu@example.com");

        var ubuntu = new Stokvel(Guid.Parse("20000000-0000-0000-0000-000000000001"),
            "Ubuntu Savers", 500m, ContributionFrequency.Monthly, 6);
        ubuntu.AddMember(thandi);
        ubuntu.AddMember(sipho);
        ubuntu.AddMember(lerato);

        var grocery = new Stokvel(Guid.Parse("20000000-0000-0000-0000-000000000002"),
            "Festive Grocery Club", 250m, ContributionFrequency.Monthly, 3);
        grocery.AddMember(thandi);
        grocery.AddMember(kagiso);
        grocery.AddMember(naledi);

        Users = [thandi, sipho, lerato, kagiso, naledi, bongani];
        Stokvels = [ubuntu, grocery];
    }
}