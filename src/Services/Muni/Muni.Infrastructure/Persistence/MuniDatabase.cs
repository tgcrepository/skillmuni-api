using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;

namespace Muni.Infrastructure.Persistence;

public static class MuniDatabase
{
    public const string HistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Applies pending migrations. Used by local runs and tests; production applies migrations from the release
    /// pipeline (an EF migration bundle) so replicas never race each other at start-up.
    /// </summary>
    public static async Task MigrateMuniDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MuniDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>MUNI_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MuniDbContext>
{
    public MuniDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MUNI_DESIGN_CONNECTION") ?? "server=localhost;database=muni";
        var options = new DbContextOptionsBuilder<MuniDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(MuniDatabase.HistoryTable))
            .Options;
        return new MuniDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
