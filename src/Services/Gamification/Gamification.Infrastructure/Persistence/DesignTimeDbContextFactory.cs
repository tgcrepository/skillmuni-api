using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Gamification.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>GAMIFICATION_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GamificationDbContext>
{
    public GamificationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("GAMIFICATION_DESIGN_CONNECTION") ?? "server=localhost;database=gamification";
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(GamificationDatabase.HistoryTable))
            .Options;
        return new GamificationDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
