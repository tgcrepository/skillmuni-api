using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Learning.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>LEARNING_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("LEARNING_DESIGN_CONNECTION") ?? "server=localhost;database=learning";
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(LearningDatabase.HistoryTable))
            .Options;
        return new LearningDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
