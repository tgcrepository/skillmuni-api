using Gamification.Application;
using Gamification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Gamification.Infrastructure.Persistence;

/// <summary>The <c>gamification</c> schema: players, the XP ledger, badges, streak days, daily quests, Arena social.</summary>
public sealed class GamificationDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public GamificationDbContext(DbContextOptions<GamificationDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Player> Players => Set<Player>();

    public DbSet<XpEntry> XpEntries => Set<XpEntry>();

    public DbSet<BadgeAward> BadgeAwards => Set<BadgeAward>();

    public DbSet<ActiveDay> ActiveDays => Set<ActiveDay>();

    public DbSet<DailyProgress> DailyProgress => Set<DailyProgress>();

    public DbSet<Cheer> Cheers => Set<Cheer>();

    public DbSet<Challenge> Challenges => Set<Challenge>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    /// <summary>Bumps <see cref="Player.Version"/> on every changed player, so concurrent writers can't lose updates.</summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Player>().Where(e => e.State == EntityState.Modified))
        {
            var version = entry.Property(p => p.Version);
            version.CurrentValue = version.OriginalValue + 1;
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GamificationDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();

        modelBuilder.Entity<Player>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<XpEntry>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<BadgeAward>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ActiveDay>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DailyProgress>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Cheer>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Challenge>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    internal sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));
}
