using FluentValidation;
using Platform.Application;
using Platform.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformApplicationServiceCollectionExtensions
{
    /// <summary>Use cases and request validators. Options are bound by the host; handlers and senders by Infrastructure.</summary>
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        services.AddScoped<IGeoService, GeoService>();
        services.AddScoped<ITenantConfigService, TenantConfigService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddValidatorsFromAssemblyContaining<UpdatePreferencesRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
