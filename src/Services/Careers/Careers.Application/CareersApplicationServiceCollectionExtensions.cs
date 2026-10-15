using Careers.Application.Contracts;
using Careers.Application.Services;
using FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

public static class CareersApplicationServiceCollectionExtensions
{
    /// <summary>Use cases, views, the matcher and validators. Options are bound by the host; handlers by Infrastructure.</summary>
    public static IServiceCollection AddCareersApplication(this IServiceCollection services)
    {
        services.AddScoped<CareerClock>();
        services.AddScoped<CareerStateLoader>();
        services.AddScoped<JobBoard>();
        services.AddScoped<FestViews>();
        services.AddSingleton<JobMatcher>();
        services.AddScoped<IJobsService, JobsService>();
        services.AddScoped<IFestService, FestService>();
        services.AddScoped<ILaunchpadService, LaunchpadService>();
        services.AddScoped<IMatchesService, MatchesService>();
        services.AddValidatorsFromAssemblyContaining<ApplyRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
