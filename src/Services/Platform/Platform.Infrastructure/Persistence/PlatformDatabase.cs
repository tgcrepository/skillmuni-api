using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;

namespace Platform.Infrastructure.Persistence;

public static class PlatformDatabase
{
    public const string HistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Applies pending migrations. Used by local runs and tests; production applies migrations from the release
    /// pipeline (an EF migration bundle) so replicas never race each other at start-up.
    /// </summary>
    public static async Task MigratePlatformDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>PLATFORM_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLATFORM_DESIGN_CONNECTION") ?? "server=localhost;database=platform";
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(PlatformDatabase.HistoryTable))
            .Options;
        return new PlatformDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
