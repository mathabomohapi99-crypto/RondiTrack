using Microsoft.EntityFrameworkCore;

namespace RondiTrack.Data; // ADAPT namespace

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
        b.Entity<Contribution>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<Payout>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<StokvelMember>()
            .HasIndex(m => new { m.StokvelId, m.RotationPosition }).IsUnique();
    }
}