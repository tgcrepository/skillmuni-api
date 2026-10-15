using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Application;
using Platform.Application.Handlers;
using Platform.Infrastructure.Geo;
using Platform.Infrastructure.Notifications;
using Platform.Infrastructure.Persistence;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Platform";

    /// <summary>MySQL, repositories, caches, the postal dataset, notification senders, the queue consumer and events.</summary>
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PlatformDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(PlatformDatabase.HistoryTable)
                .EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PlatformDbContext>());
        services.AddScoped<TenantThemeRepository>();
        services.AddScoped<ITenantThemeRepository, CachedTenantThemeRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPreferencesRepository, PreferencesRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IPlatformDataEraser, PlatformDataEraser>();

        // Postal starter dataset, cached in Redis (IDistributedCache comes from AddSkillMuniServiceDefaults).
        services.AddSingleton<EmbeddedPostalDirectory>();
        services.AddScoped<IPostalDirectory, CachedPostalDirectory>();

        // One sender per channel. Providers are open (BRD §9), so every channel uses the sandbox for now; a real
        // provider replaces the registration for its channel.
        foreach (var channel in NotificationChannels.All)
        {
            services.AddSingleton<INotificationSender>(sp => new SandboxNotificationSender(channel, sp.GetRequiredService<ILogger<SandboxNotificationSender>>()));
        }

        services.AddSkillMuniMessaging<PlatformDbContext>();
        services.AddSkillMuniEventConsumer<PlatformDbContext>();
        services.AddIntegrationEventHandler<LearnerErased, LearnerErasedHandler>(LearnerErased.Name);
        services.AddIntegrationEventHandler<BadgeEarned, BadgeEarnedHandler>(BadgeEarned.Name);

        services.AddSingleton<NotificationQueueConsumer>();
        if (configuration.GetValue("Platform:Notifications:QueueEnabled", true))
        {
            services.AddHostedService(sp => sp.GetRequiredService<NotificationQueueConsumer>());
        }

        return services;
    }

    /// <summary>Every event type the "platform" subscription receives (kept with the handler list).</summary>
    public static readonly IReadOnlyList<string> SubscribedEvents = [LearnerErased.Name, BadgeEarned.Name];
}
