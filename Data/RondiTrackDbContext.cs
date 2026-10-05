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
        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.FullName).HasMaxLength(User.MaxNameLength).IsRequired();
            e.Property(u => u.Email).HasMaxLength(320).IsRequired();
            e.Property(u => u.Version).IsRowVersion();   // ADDED 5.3: xmin concurrency token
        });

        b.Entity<Stokvel>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(Stokvel.MaxNameLength).IsRequired();
            e.Property(s => s.ContributionAmount).HasPrecision(18, 2);
            e.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Version).IsRowVersion();   // ADDED 5.3: xmin concurrency token

            // Members is a read-only wrapper over a private list of Users, so EF Core cannot fill it.
            // Membership is persisted through StokvelMember (the Memberships navigation) instead.
            e.Ignore(s => s.Members);
            e.Ignore(s => s.PayoutPerCycle); // calculated
            e.Ignore(s => s.IsFull);         // calculated
        });

        // StokvelMember now has a COMPOSITE primary key and two real relationships
        b.Entity<StokvelMember>(e =>
        {
            e.HasKey(m => new { m.UserId, m.StokvelId });   // composite natural key
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            // the old unique index on (StokvelId, UserId) is removed: the primary key does that job now
            e.HasIndex(m => new { m.StokvelId, m.RotationPosition }).IsUnique();

            e.HasOne(m => m.User)
             .WithMany(u => u.Memberships)
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Stokvel)
             .WithMany(s => s.Memberships)
             .HasForeignKey(m => m.StokvelId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ContributionCycle>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.TargetAmount).HasPrecision(18, 2);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Version).IsRowVersion();   // ADDED 5.3: xmin concurrency token
            // NOTE 5.3: NOT unique on purpose. "One cycle number per stokvel" is not checked in C# today,
            // so it is written up as a gap in the README instead of being added as a new rule.
            e.HasIndex(c => new { c.StokvelId, c.CycleNumber });

            // The second one-to-many (Stokvel -> ContributionCycle)
            e.HasOne(c => c.Stokvel)
             .WithMany(s => s.Cycles)
             .HasForeignKey(c => c.StokvelId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Contribution>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Amount).HasPrecision(18, 2);

            // CHANGED 5.3: now UNIQUE. Database-level version of the C# rule
            // "a member contributes once per cycle" (ContributionService -> ExistsAsync).
            e.HasIndex(c => new { c.CycleId, c.UserId }).IsUnique();

            // ADDED 5.3 (stage 2): composite index for the paged contributions query.
            // CycleId first (equality filter), then RecordedAtUtc (the sort), then Id (the tiebreaker),
            // so Postgres reads one cycle's rows already in order and can stop after LIMIT.
            e.HasIndex(c => new { c.CycleId, c.RecordedAtUtc, c.Id })
             .HasDatabaseName("IX_Contributions_CycleId_RecordedAtUtc_Id");

            // Composite foreign key to ONE specific membership (same order as the primary key)
            e.HasOne(c => c.Member)
             .WithMany(m => m.Contributions)
             .HasForeignKey(c => new { c.UserId, c.StokvelId })
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Payout>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.Version).IsRowVersion();   // ADDED 5.3: xmin concurrency token
            e.HasIndex(p => p.CycleId).IsUnique(); // a cycle can only be paid out once

            // ADDED 5.3: a member is paid at most once per stokvel rotation.
            // Database-level version of the C# rule in PayoutService ("Every member has already been paid").
            e.HasIndex(p => new { p.StokvelId, p.RecipientUserId }).IsUnique();

            // EDIT 5.2: the recipient is one specific membership (composite foreign key, no navigation property)
            e.HasOne<StokvelMember>()
             .WithMany()
             .HasForeignKey(p => new { p.RecipientUserId, p.StokvelId })
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}