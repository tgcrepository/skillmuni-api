using Assessment.Application;
using Assessment.Application.Services;
using FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

public static class AssessmentApplicationServiceCollectionExtensions
{
    /// <summary>Use cases and validators. Options are bound by the host; event handlers and content by Infrastructure.</summary>
    public static IServiceCollection AddAssessmentApplication(this IServiceCollection services)
    {
        services.AddScoped<ICareerAssessmentService, CareerAssessmentService>();
        services.AddSingleton<IAssessmentStatsService, AssessmentStatsService>();
        services.AddValidatorsFromAssemblyContaining<SaveAnswersRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
