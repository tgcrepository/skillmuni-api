using FluentValidation;
using Gamification.Application;
using Gamification.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class GamificationApplicationServiceCollectionExtensions
{
    /// <summary>The engine, use cases and request validators. Options are bound by the host; event handlers by Infrastructure.</summary>
    public static IServiceCollection AddGamificationApplication(this IServiceCollection services)
    {
        services.AddScoped<IGameEngine, GameEngine>();
        services.AddScoped<IArenaService, ArenaService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddValidatorsFromAssemblyContaining<ShowcaseRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
