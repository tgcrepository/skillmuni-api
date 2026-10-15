using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SkillMuni.Common.Tenancy;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// For <c>dotnet ef migrations add</c>. Building a migration never connects, so no credentials are needed;
/// set <c>IDENTITY_DESIGN_CONNECTION</c> only to run <c>dotnet ef database update</c> by hand.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_DESIGN_CONNECTION") ?? "server=localhost;database=identity";
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySQL(connection, my => my.MigrationsHistoryTable(IdentityDatabase.HistoryTable))
            .Options;
        return new IdentityDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";

        public string Market => "IN";
    }
}
