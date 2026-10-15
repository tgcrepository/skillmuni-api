using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;
using SkillMuni.Messaging.Publishing;

namespace Microsoft.Extensions.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Outbox on <typeparamref name="TContext"/>, the background dispatcher and the Service Bus publisher.</summary>
    public static IServiceCollection AddSkillMuniMessaging<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddOptions<ServiceBusOptions>().BindConfiguration(ServiceBusOptions.Section);
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.Section);
        services.AddSingleton<ServiceBusClientProvider>();
        services.AddSingleton<IEventBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClientProvider>().Client;
            return client is null
                ? new LoggingEventPublisher(sp.GetRequiredService<ILogger<LoggingEventPublisher>>())
                : new ServiceBusEventPublisher(client, sp.GetRequiredService<IOptions<ServiceBusOptions>>());
        });
        services.AddScoped<IOutbox, EfOutbox<TContext>>();
        services.AddHostedService<OutboxDispatcher<TContext>>();
        return services;
    }

    /// <summary>
    /// Subscribes the service to events: inbox on <typeparamref name="TContext"/>, the dispatcher and the
    /// Service Bus consumer. Register handlers with <see cref="AddIntegrationEventHandler{TEvent, THandler}"/>.
    /// </summary>
    public static IServiceCollection AddSkillMuniEventConsumer<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddSingleton<EventHandlerRegistry>();
        services.AddSingleton<IntegrationEventDispatcher<TContext>>();
        services.AddHostedService<ServiceBusEventConsumer<TContext>>();
        return services;
    }

    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services, string eventType)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new HandlerRegistration(registry => registry.Register<TEvent, THandler>(eventType)));
        return services;
    }
}
