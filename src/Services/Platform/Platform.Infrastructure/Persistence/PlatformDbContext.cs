using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Platform.Application;
using Platform.Domain;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Platform.Infrastructure.Persistence;

/// <summary>The <c>platform</c> schema: tenant themes, bell notifications, channel preferences and delivery records.</summary>
public sealed class PlatformDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<TenantTheme> Themes => Set<TenantTheme>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreferences> Preferences => Set<NotificationPreferences>();

    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();
        modelBuilder.AddErasureTombstones();

        modelBuilder.Entity<TenantTheme>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<NotificationPreferences>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<NotificationDelivery>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
