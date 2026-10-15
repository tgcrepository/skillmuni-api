using Azure.Identity;
using Microsoft.Azure.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace SkillMuni.Web.Caching;

/// <summary>
/// The shared <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> (CLAUDE.md §8), one per service:
/// <list type="bullet">
/// <item><c>Redis:Host</c> set (Azure): Azure Cache for Redis with Microsoft Entra sign-in through the app's managed
/// identity. Access keys are disabled on the cache, so no secret exists.</item>
/// <item>Else <c>ConnectionStrings:Redis</c> (local docker compose): plain connection string.</item>
/// <item>Else per-replica memory (tests and quick local runs).</item>
/// </list>
/// Keys are prefixed with the service name, so services sharing one cache never read each other's entries.
/// </summary>
public static class CachingExtensions
{
    public const string HostKey = "Redis:Host";
    public const string ConnectionStringName = "Redis";

    public static IServiceCollection AddSkillMuniCache(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var instanceName = $"skillmuni:{serviceName}:";
        var host = configuration[HostKey];
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (!string.IsNullOrWhiteSpace(host))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.InstanceName = instanceName;
                o.ConnectionMultiplexerFactory = async () =>
                {
                    var options = ConfigurationOptions.Parse(host);
                    options.Ssl = true;
                    options.AbortOnConnectFail = false;
                    await options.ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential());
                    return await ConnectionMultiplexer.ConnectAsync(options);
                };
            });
        }
        else if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = connectionString;
                o.InstanceName = instanceName;
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}
