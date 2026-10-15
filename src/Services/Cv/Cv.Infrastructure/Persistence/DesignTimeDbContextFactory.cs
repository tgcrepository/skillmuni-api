using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Cv.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>CV_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CvDbContext>
{
    public CvDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CV_DESIGN_CONNECTION") ?? "server=localhost;database=cv";
        var options = new DbContextOptionsBuilder<CvDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(CvDatabase.HistoryTable))
            .Options;
        return new CvDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
