using FluentValidation;
using Learner.Application;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Learner.Domain;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class LearnerApplicationServiceCollectionExtensions
{
    /// <summary>Use cases, the quest catalogue and request validators. Options are bound by the host.</summary>
    public static IServiceCollection AddLearnerApplication(this IServiceCollection services)
    {
        services.AddSingleton(sp => new QuestCatalog(sp.GetRequiredService<IOptions<LearnerServiceOptions>>().Value.QuestCatalog()));
        services.AddScoped<ILearnerAccessor, LearnerAccessor>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IDraftService, DraftService>();
        services.AddScoped<IIdentityLinkService, IdentityLinkService>();
        services.AddScoped<IProgrammeService, ProgrammeService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ITrophyService, TrophyService>();
        services.AddScoped<ISkillSearchService, SkillSearchService>();
        services.AddScoped<IPreferencesService, PreferencesService>();
        services.AddScoped<IProfileProgressService, ProfileProgressService>();
        services.AddScoped<ICareersProfileService, CareersProfileService>();
        services.AddScoped<IJobProfileCompletion, JobProfileCompletion>();
        services.AddScoped<IJobProfileService, JobProfileService>();
        services.AddValidatorsFromAssemblyContaining<SavePartRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
