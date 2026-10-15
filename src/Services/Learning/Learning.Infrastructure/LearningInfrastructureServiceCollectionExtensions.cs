using Learning.Application;
using Learning.Application.Handlers;
using Learning.Infrastructure.Content;
using Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class LearningInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Learning";

    /// <summary>MySQL, repositories, the seed catalogue, outbox and every event handler.</summary>
    public static IServiceCollection AddLearningInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LearningDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(LearningDatabase.HistoryTable)
                .EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LearningDbContext>());
        services.AddScoped<IBriefProgressRepository, BriefProgressRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<ILobbyRepository, LobbyRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ILearnerDataEraser, LearnerDataEraser>();

        // Static catalogue, built once per process (in memory; no Redis needed until a CMS provider replaces it).
        services.AddSingleton<ILearningContent, SeedLearningContent>();

        services.AddSkillMuniMessaging<LearningDbContext>();
        services.AddSkillMuniEventConsumer<LearningDbContext>();
        services.AddErasureTombstones<LearningDbContext>();
        services.AddIntegrationEventHandler<AccountErased, AccountErasedHandler>(AccountErased.Name);
        services.AddIntegrationEventHandler<AssessmentSubmitted, AssessmentSubmittedHandler>(AssessmentSubmitted.Name);
        return services;
    }

    /// <summary>Every event type the "learning" subscription receives (kept with the handler list).</summary>
    public static readonly IReadOnlyList<string> SubscribedEvents = [AccountErased.Name, AssessmentSubmitted.Name];
}
