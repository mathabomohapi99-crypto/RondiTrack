using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class RondiTrackDbContext : DbContext
{
    public RondiTrackDbContext(DbContextOptions<RondiTrackDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payout> Payouts => Set<Payout>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Stokvel>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(Stokvel.MaxNameLength).IsRequired();
            e.Property(s => s.ContributionAmount).HasPrecision(18, 2);
            e.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(20);

            // Members is a read-only wrapper over a private list, so EF Core cannot fill it.
            // Membership is persisted through StokvelMember instead.
            e.Ignore(s => s.Members);
            e.Ignore(s => s.PayoutPerCycle); // calculated
            e.Ignore(s => s.IsFull);         // calculated
        });

        b.Entity<Contribution>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Amount).HasPrecision(18, 2);
            e.HasIndex(c => new { c.CycleId, c.UserId });
        });

        b.Entity<StokvelMember>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.StokvelId, m.UserId }).IsUnique();
            e.HasIndex(m => new { m.StokvelId, m.RotationPosition }).IsUnique();
        });

        b.Entity<Payout>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.HasIndex(p => p.CycleId).IsUnique(); // a cycle can only be paid out once
        });

        // User and ContributionCycle: I'll add their configuration once I see those files.
    }
}