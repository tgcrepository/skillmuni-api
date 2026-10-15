using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Storage;
using SkillMuni.Storage;

namespace Microsoft.Extensions.DependencyInjection;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IBlobStore"/> from <paramref name="section"/> (for example <c>Learner:Storage</c>). Outside
    /// Development and Testing, a ServiceUri or ConnectionString is required at start-up.
    /// </summary>
    public static IServiceCollection AddSkillMuniBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string section,
        string defaultContainer)
    {
        var isLocal = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        services.AddOptions<BlobStorageOptions>()
            .BindConfiguration(section)
            .PostConfigure(o => o.Container = string.IsNullOrWhiteSpace(o.Container) ? defaultContainer : o.Container)
            .Validate(o => isLocal || o.IsConfigured, $"{section}:ServiceUri is required outside Development and Testing.")
            .ValidateOnStart();

        var settings = configuration.GetSection(section).Get<BlobStorageOptions>() ?? new BlobStorageOptions();
        if (settings.IsConfigured)
        {
            services.AddSingleton<IBlobStore, AzureBlobStore>();
        }
        else
        {
            services.AddSingleton<IBlobStore, InMemoryBlobStore>();
        }

        return services;
    }
}
