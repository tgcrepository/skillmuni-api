using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Learner.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>LEARNER_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LearnerDbContext>
{
    public LearnerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("LEARNER_DESIGN_CONNECTION") ?? "server=localhost;database=learner";
        var options = new DbContextOptionsBuilder<LearnerDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(LearnerDatabase.HistoryTable))
            .Options;
        return new LearnerDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
