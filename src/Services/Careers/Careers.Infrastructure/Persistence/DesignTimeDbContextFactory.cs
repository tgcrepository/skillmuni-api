using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Careers.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>CAREERS_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareersDbContext>
{
    public CareersDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CAREERS_DESIGN_CONNECTION") ?? "server=localhost;database=careers";
        var options = new DbContextOptionsBuilder<CareersDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(CareersDatabase.HistoryTable))
            .Options;
        return new CareersDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
