using Learner.Application.Abstractions;
using Learner.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Learner.Infrastructure.Persistence;

/// <summary>
/// The <c>learner</c> schema. Every tenant-owned table has a global query filter on <c>tenant_id</c> (CLAUDE.md §6);
/// the outbox and inbox live here too, so events commit with the change they describe or cause.
/// </summary>
public sealed class LearnerDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public LearnerDbContext(DbContextOptions<LearnerDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<LearnerProfile> Learners => Set<LearnerProfile>();

    public DbSet<QuestAnswers> QuestAnswers => Set<QuestAnswers>();

    public DbSet<Draft> Drafts => Set<Draft>();

    public DbSet<LinkedIdentity> LinkedIdentities => Set<LinkedIdentity>();

    public DbSet<Programme> Programmes => Set<Programme>();

    public DbSet<Enrolment> Enrolments => Set<Enrolment>();

    public DbSet<Feedback> Feedback => Set<Feedback>();

    public DbSet<TrophyFile> TrophyFiles => Set<TrophyFile>();

    public DbSet<JobProfileSettings> JobProfiles => Set<JobProfileSettings>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Read by the query filters on every query, so each request or event sees only its own tenant.</summary>
    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Stored as UTC DATETIME(6); read back marked as UTC so JSON carries the "Z". Covers DateTime? too.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");

        // DATE columns, converted explicitly so the mapping doesn't depend on the provider's DateOnly support.
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearnerDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();
        modelBuilder.AddErasureTombstones();

        modelBuilder.Entity<LearnerProfile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<QuestAnswers>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Draft>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LinkedIdentity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Programme>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Enrolment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Feedback>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<TrophyFile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<JobProfileSettings>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    internal sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));
}
