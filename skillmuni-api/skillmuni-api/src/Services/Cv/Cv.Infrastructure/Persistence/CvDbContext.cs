using Cv.Application;
using Cv.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Cv.Infrastructure.Persistence;

/// <summary>The <c>cv</c> schema: CV documents and their parse jobs, plus the outbox and inbox.</summary>
public sealed class CvDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public CvDbContext(DbContextOptions<CvDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<CvDocument> Documents => Set<CvDocument>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Read by the query filter on every query, so each request or job sees only its own tenant.</summary>
    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CvDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();
        modelBuilder.Entity<CvDocument>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
