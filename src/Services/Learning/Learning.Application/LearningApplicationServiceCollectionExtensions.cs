using FluentValidation;
using Learning.Application.Contracts;
using Learning.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class LearningApplicationServiceCollectionExtensions
{
    /// <summary>Use cases and request validators. Options are bound by the host; content and handlers by Infrastructure.</summary>
    public static IServiceCollection AddLearningApplication(this IServiceCollection services)
    {
        services.AddScoped<ILearnService, LearnService>();
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<ILearningStatsService, LearningStatsService>();
        services.AddValidatorsFromAssemblyContaining<ActivityRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
