using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Assessment.Infrastructure.Persistence;

public static class AssessmentDatabase
{
    public const string HistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Applies pending migrations. Used by local runs and tests; production applies migrations from the release
    /// pipeline (an EF migration bundle) so replicas never race each other at start-up.
    /// </summary>
    public static async Task MigrateAssessmentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
