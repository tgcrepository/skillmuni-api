using Identity.Application.Abstractions;
using Identity.Domain.Accounts;
using Identity.Domain.Claims;
using Identity.Domain.Codes;
using Identity.Domain.Consents;
using Identity.Domain.Registration;
using Identity.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Outbox;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// The <c>identity</c> schema. Every tenant-owned table has a global query filter on <c>tenant_id</c>
/// (CLAUDE.md §6), and the outbox lives here too so events commit with the change.
/// </summary>
public sealed class IdentityDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<ConsentRecord> Consents => Set<ConsentRecord>();

    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();

    public DbSet<ClaimInvite> ClaimInvites => Set<ClaimInvite>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Read by the query filters on every query, so each request sees only its own tenant.</summary>
    private string CurrentTenantId => _tenant.TenantId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Stored as UTC DATETIME(6); read back marked as UTC so JSON carries the "Z".
        // Pre-convention configuration of a value type also covers its nullable form (DateTime?).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("datetime(6)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.AddOutbox();

        modelBuilder.Entity<Account>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<VerificationCode>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Session>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ConsentRecord>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<PendingRegistration>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ClaimInvite>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    internal sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
