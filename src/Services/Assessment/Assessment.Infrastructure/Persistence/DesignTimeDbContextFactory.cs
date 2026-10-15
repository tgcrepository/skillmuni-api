using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Assessment.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>ASSESSMENT_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AssessmentDbContext>
{
    public AssessmentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ASSESSMENT_DESIGN_CONNECTION") ?? "server=localhost;database=assessment";
        var options = new DbContextOptionsBuilder<AssessmentDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(AssessmentDatabase.HistoryTable))
            .Options;
        return new AssessmentDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
