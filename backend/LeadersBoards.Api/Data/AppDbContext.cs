using LeadersBoards.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeadersBoards.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Leaderboard> Leaderboards => Set<Leaderboard>();
    public DbSet<LeaderboardEvent> Events => Set<LeaderboardEvent>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<EventView> EventViews => Set<EventView>();
    public DbSet<Heart> Hearts => Set<Heart>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Store every enum as text ("Time", "Accepted", ...) rather than an int.
        b.Properties<ScoreType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<SortDirection>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<LeaderboardStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<MemberRole>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<MembershipStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<LeaderboardKind>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<Visibility>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<BoardPlan>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<PaymentKind>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.DisplayName).HasMaxLength(80);
        });

        b.Entity<Leaderboard>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Source).HasMaxLength(20);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Visibility, x.Status }); // public board discovery
            e.HasIndex(x => x.ExpiresAt).HasFilter("expires_at IS NOT NULL"); // cleanup job
        });

        b.Entity<LeaderboardEvent>(e =>
        {
            e.ToTable("events");
            e.Property(x => x.Name).HasMaxLength(120);
            e.HasOne(x => x.Leaderboard).WithMany(l => l.Events).HasForeignKey(x => x.LeaderboardId);
            e.HasIndex(x => new { x.LeaderboardId, x.Position });
            e.HasIndex(x => new { x.LeaderboardId, x.ScheduledOn }); // "today's WOD"
        });

        b.Entity<Membership>(e =>
        {
            e.HasKey(x => new { x.LeaderboardId, x.UserId });
            e.HasOne(x => x.Leaderboard).WithMany(l => l.Memberships).HasForeignKey(x => x.LeaderboardId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.Status }); // "my boards" and "my invites" lookups
            e.Ignore(x => x.IsAdmin);
        });

        b.Entity<Score>(e =>
        {
            e.Property(x => x.PrimaryValue).HasPrecision(12, 3);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Event).WithMany(ev => ev.Scores).HasForeignKey(x => x.EventId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasIndex(x => new { x.EventId, x.UserId }).IsUnique(); // one score per person per event
        });

        b.Entity<EventView>(e =>
        {
            e.HasKey(x => new { x.EventId, x.UserId });
            e.HasOne<LeaderboardEvent>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<Heart>(e =>
        {
            e.HasKey(x => new { x.ScoreId, x.UserId });
            e.HasOne(x => x.Score).WithMany(s => s.Hearts).HasForeignKey(x => x.ScoreId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<Comment>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(1000);
            e.HasOne<LeaderboardEvent>().WithMany().HasForeignKey(x => x.EventId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasIndex(x => new { x.EventId, x.CreatedAt });
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.ProviderRef).HasMaxLength(255);
            // Deleting a board keeps its payments (board id becomes null) for the accounts.
            e.HasOne(x => x.Leaderboard).WithMany().HasForeignKey(x => x.LeaderboardId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.LeaderboardId);
            e.HasIndex(x => new { x.Provider, x.ProviderRef }).IsUnique(); // webhook idempotency
        });
    }
}
