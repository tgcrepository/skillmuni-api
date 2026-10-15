using Gamification.Application;
using Gamification.Application.Handlers;
using Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class GamificationInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Gamification";
    /// <summary>MySQL, repositories, the board cache, outbox and every event handler.</summary>
    public static IServiceCollection AddGamificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GamificationDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(GamificationDatabase.HistoryTable)
                .EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GamificationDbContext>());
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IXpLedger, XpLedger>();
        services.AddScoped<IBadgeAwardRepository, BadgeAwardRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ISocialRepository, SocialRepository>();
        services.AddScoped<IPlayerDataEraser, PlayerDataEraser>();
        services.AddScoped<BoardQueries>();
        services.AddScoped<IBoardQueries, CachedBoardQueries>();

        // IDistributedCache (Redis in Azure) comes from AddSkillMuniServiceDefaults.

        services.AddSkillMuniMessaging<GamificationDbContext>();
        services.AddSkillMuniEventConsumer<GamificationDbContext>();
        services.AddErasureTombstones<GamificationDbContext>();
        services.AddIntegrationEventHandler<AccountRegistered, AccountRegisteredHandler>(AccountRegistered.Name);
        services.AddIntegrationEventHandler<AccountErased, AccountErasedHandler>(AccountErased.Name);
        services.AddIntegrationEventHandler<QuestStepCompleted, QuestStepHandler>(QuestStepCompleted.Name);
        services.AddIntegrationEventHandler<QuestCompleted, QuestCompletedHandler>(QuestCompleted.Name);
        services.AddIntegrationEventHandler<ProfileTaskCompleted, ProfileTaskHandler>(ProfileTaskCompleted.Name);
        services.AddIntegrationEventHandler<ProgrammeEnrolled, ProgrammeEnrolledHandler>(ProgrammeEnrolled.Name);
        services.AddIntegrationEventHandler<FeedbackSent, FeedbackSentHandler>(FeedbackSent.Name);
        services.AddIntegrationEventHandler<PhotoChanged, PhotoChangedHandler>(PhotoChanged.Name);
        services.AddIntegrationEventHandler<AssessmentSubmitted, AssessmentSubmittedHandler>(AssessmentSubmitted.Name);
        services.AddIntegrationEventHandler<LearningActivity, LearningActivityHandler>(LearningActivity.Name);
        services.AddIntegrationEventHandler<BriefCompleted, BriefCompletedHandler>(BriefCompleted.Name);
        services.AddIntegrationEventHandler<QuizSubmitted, QuizSubmittedHandler>(QuizSubmitted.Name);
        services.AddIntegrationEventHandler<ModuleCompleted, ModuleCompletedHandler>(ModuleCompleted.Name);
        services.AddIntegrationEventHandler<CourseCompleted, CourseCompletedHandler>(CourseCompleted.Name);
        services.AddIntegrationEventHandler<JobProfileCompleted, JobProfileCompletedHandler>(JobProfileCompleted.Name);
        services.AddIntegrationEventHandler<IslandCleared, IslandClearedHandler>(IslandCleared.Name);
        services.AddIntegrationEventHandler<FirstMovesClaimed, FirstMovesClaimedHandler>(FirstMovesClaimed.Name);
        services.AddIntegrationEventHandler<ApplicationSent, ApplicationSentHandler>(ApplicationSent.Name);
        services.AddIntegrationEventHandler<FestBooked, FestBookedHandler>(FestBooked.Name);
        return services;
    }

    /// <summary>Every event type the "gamification" subscription receives (kept with the handler list and main.bicep).</summary>
    public static readonly IReadOnlyList<string> SubscribedEvents =
    [
        AccountRegistered.Name, AccountErased.Name, QuestStepCompleted.Name, QuestCompleted.Name,
        ProfileTaskCompleted.Name, ProgrammeEnrolled.Name, FeedbackSent.Name, PhotoChanged.Name, AssessmentSubmitted.Name,
        LearningActivity.Name, BriefCompleted.Name, QuizSubmitted.Name, ModuleCompleted.Name, CourseCompleted.Name,
        JobProfileCompleted.Name, IslandCleared.Name, FirstMovesClaimed.Name, ApplicationSent.Name, FestBooked.Name,
    ];
}
