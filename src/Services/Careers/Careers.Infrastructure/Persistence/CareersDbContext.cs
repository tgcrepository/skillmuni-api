using Careers.Application.Abstractions;
using Careers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Careers.Infrastructure.Persistence;

/// <summary>
/// The <c>careers</c> schema: per-learner state only (bookmarks, applications, CVs, invitations, fest tickets,
/// waitlists, outcomes, ratings, match actions, settings and event projections). The catalogue is seed data.
/// </summary>
public sealed class CareersDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public CareersDbContext(DbContextOptions<CareersDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<CareerLearner> Learners => Set<CareerLearner>();

    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();

    public DbSet<JobApplication> Applications => Set<JobApplication>();

    public DbSet<CvFile> CvFiles => Set<CvFile>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<FestBooking> FestBookings => Set<FestBooking>();

    public DbSet<FestPick> FestPicks => Set<FestPick>();

    public DbSet<FestWaitlistEntry> FestWaitlist => Set<FestWaitlistEntry>();

    public DbSet<FestOutcome> FestOutcomes => Set<FestOutcome>();

    public DbSet<FestRating> FestRatings => Set<FestRating>();

    public DbSet<SavedFest> SavedFests => Set<SavedFest>();

    public DbSet<MatchState> MatchStates => Set<MatchState>();

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareersDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();
        modelBuilder.AddErasureTombstones();

        modelBuilder.Entity<CareerLearner>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SavedJob>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<JobApplication>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<CvFile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Invitation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FestBooking>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FestPick>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FestWaitlistEntry>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FestOutcome>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<FestRating>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SavedFest>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<MatchState>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    internal sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));
}
