using Microsoft.EntityFrameworkCore;

namespace SitePulse.Api.Data;

/// <summary>
/// Mirrors db/seed/schema.sql: snake_case names, ids supplied by the seed, UTC timestamps
/// stored as datetime2(0), varchar lengths as in the starter.
/// </summary>
public class SitePulseDbContext(DbContextOptions<SitePulseDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(a => a.Name).HasColumnName("name").HasColumnType("varchar(120)").IsRequired();
            e.Property(a => a.Industry).HasColumnName("industry").HasColumnType("varchar(60)").IsRequired();
            e.Property(a => a.Timezone).HasColumnName("timezone").HasColumnType("varchar(60)").IsRequired();
            e.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2(0)");
        });

        modelBuilder.Entity<ActivityEvent>(e =>
        {
            e.ToTable("activity_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Location).HasColumnName("location").HasColumnType("varchar(80)").IsRequired();
            e.Property(x => x.EventType).HasColumnName("event_type").HasColumnType("varchar(40)").IsRequired();
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("datetime2(0)");
            e.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
            e.Property(x => x.Outcome).HasColumnName("outcome").HasColumnType("varchar(40)");

            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);

            // Every dashboard read is "one account, a UTC range": seek on both, cover the grouping columns.
            e.HasIndex(x => new { x.AccountId, x.OccurredAt })
                .HasDatabaseName("ix_activity_events_account_occurred")
                .IncludeProperties(x => new { x.Location, x.EventType });
        });
    }
}
