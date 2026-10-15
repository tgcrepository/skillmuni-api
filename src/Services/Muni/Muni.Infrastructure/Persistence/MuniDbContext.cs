using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Muni.Application;
using Muni.Domain;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Muni.Infrastructure.Persistence;

/// <summary>The <c>muni</c> schema: learner copies (from events), chat history and reply ratings.</summary>
public sealed class MuniDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public MuniDbContext(DbContextOptions<MuniDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<MuniLearner> Learners => Set<MuniLearner>();

    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    public DbSet<ReplyRating> Ratings => Set<ReplyRating>();

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MuniDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.AddInbox();

        modelBuilder.Entity<MuniLearner>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ChatMessage>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ReplyRating>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
