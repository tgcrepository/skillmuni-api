using FluentValidation;
using Muni.Application;
using Muni.Application.Handlers;
using Muni.Application.Responders;
using Muni.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class MuniApplicationServiceCollectionExtensions
{
    /// <summary>Use cases, the keyword responder and request validators. Options are bound by the host; handlers by Infrastructure.</summary>
    public static IServiceCollection AddMuniApplication(this IServiceCollection services)
    {
        services.AddScoped<ILearnerSnapshotBuilder, LearnerSnapshotBuilder>();
        services.AddScoped<IMuniResponder, KeywordMuniResponder>();
        services.AddScoped<IMuniService, MuniService>();
        services.AddScoped<MuniLearnerAccessor>();
        services.AddValidatorsFromAssemblyContaining<AskRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
