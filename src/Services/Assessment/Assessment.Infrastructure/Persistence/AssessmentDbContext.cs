using Assessment.Application;
using Assessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Assessment.Infrastructure.Persistence;

/// <summary>The <c>assessment</c> schema: each learner's Career Drivers journey and submitted attempts, plus outbox and inbox.</summary>
public sealed class AssessmentDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public AssessmentDbContext(DbContextOptions<AssessmentDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<CareerJourney> Journeys => Set<CareerJourney>();

    public DbSet<CareerAttempt> Attempts => Set<CareerAttempt>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Read by the query filter on every query, so each request or event sees only its own tenant.</summary>
    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssessmentDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();
        modelBuilder.AddErasureTombstones();

        modelBuilder.Entity<CareerJourney>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<CareerAttempt>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    internal sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));
}
