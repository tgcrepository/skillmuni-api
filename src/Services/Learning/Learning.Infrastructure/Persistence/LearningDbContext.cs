using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Learning.Infrastructure.Persistence;

/// <summary>The <c>learning</c> schema: brief progress, quiz attempts and drafts, the Lobby's first moves, learning minutes.</summary>
public sealed class LearningDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public LearningDbContext(DbContextOptions<LearningDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<BriefProgress> BriefProgress => Set<BriefProgress>();

    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    public DbSet<QuizDraft> QuizDrafts => Set<QuizDraft>();

    public DbSet<LobbyState> LobbyStates => Set<LobbyState>();

    public DbSet<ActivityDay> ActivityDays => Set<ActivityDay>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();

        modelBuilder.Entity<BriefProgress>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<QuizAttempt>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<QuizDraft>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<LobbyState>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ActivityDay>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    internal sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));
}
