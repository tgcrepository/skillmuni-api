using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace SkillMuni.Messaging.Publishing;

/// <summary>Holds the one <see cref="ServiceBusClient"/> per process, or none when Service Bus isn't configured.</summary>
public sealed class ServiceBusClientProvider : IAsyncDisposable
{
    public ServiceBusClientProvider(IOptions<ServiceBusOptions> options)
    {
        var settings = options.Value;
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            Client = new ServiceBusClient(settings.ConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(settings.FullyQualifiedNamespace))
        {
            Client = new ServiceBusClient(settings.FullyQualifiedNamespace, new DefaultAzureCredential());
        }
    }

    public ServiceBusClient? Client { get; }

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }
    }
}
