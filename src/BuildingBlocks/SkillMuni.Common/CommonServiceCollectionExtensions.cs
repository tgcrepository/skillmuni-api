using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class CommonServiceCollectionExtensions
{
    /// <summary>Registers the shared clock (<see cref="TimeProvider"/>). Tests swap it for a fake.</summary>
    public static IServiceCollection AddSkillMuniCommon(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
