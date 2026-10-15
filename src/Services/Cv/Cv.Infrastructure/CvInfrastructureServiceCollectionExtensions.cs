using Cv.Application;
using Cv.Application.Services;
using Cv.Infrastructure.Persistence;
using Cv.Infrastructure.Reading;
using Cv.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class CvInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Cv";

    /// <summary>MySQL, Blob Storage, the text reader, the Identity lookup, the parse worker, outbox and event consumer.</summary>
    public static IServiceCollection AddCvInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var isLocal = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        services.AddDbContext<CvDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(CvDatabase.HistoryTable)
                .EnableRetryOnFailure(3));

            // The first migration was generated without the SDK; CI checks the snapshot instead of failing start-up.
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CvDbContext>());
        services.AddScoped<ICvRepository, CvRepository>();
        services.AddScoped<ICvJobQueue, CvJobQueue>();

        services.AddSkillMuniBlobStorage(configuration, environment, "Cv:Storage", "cv-files");

        // Text reading: Document Intelligence in Azure (managed identity); the sample-CV sandbox locally.
        services.AddOptions<CvReaderOptions>()
            .BindConfiguration(CvReaderOptions.Section)
            .Validate(o => o.Mode != CvReaderOptions.Sandbox || isLocal, "Cv:Reader:Mode=sandbox is allowed only in Development and Testing.")
            .Validate(o => o.Mode == CvReaderOptions.Sandbox || !string.IsNullOrWhiteSpace(o.Endpoint), "Cv:Reader:Endpoint is required for Document Intelligence.")
            .ValidateOnStart();
        var reader = configuration.GetSection(CvReaderOptions.Section).Get<CvReaderOptions>() ?? new CvReaderOptions();
        var sandbox = reader.Mode == CvReaderOptions.Sandbox && isLocal;
        if (sandbox)
        {
            services.AddSingleton<ICvTextReader, SandboxCvTextReader>();
        }
        else
        {
            services.AddSingleton<ICvTextReader, DocumentIntelligenceTextReader>();
        }

        // S9 email check against Identity (internal ingress), with retry, timeout and circuit breaker.
        var identity = configuration["Cv:IdentityBaseAddress"];
        if (sandbox && string.IsNullOrWhiteSpace(identity))
        {
            services.AddSingleton<IAccountDirectory, SandboxAccountDirectory>();
        }
        else
        {
            services.AddHttpClient<IAccountDirectory, IdentityAccountDirectory>(c => c.BaseAddress = new Uri(identity ?? "http://identity:8080/"))
                .AddStandardResilienceHandler();
        }

        services.AddSingleton<CvParseSignal>();
        services.AddSingleton<ICvParseSignal>(sp => sp.GetRequiredService<CvParseSignal>());
        if (configuration.GetValue("Cv:Worker:Enabled", true))
        {
            services.AddHostedService<CvParseWorker>();
        }

        services.AddSkillMuniMessaging<CvDbContext>();
        services.AddSkillMuniEventConsumer<CvDbContext>();
        services.AddIntegrationEventHandler<AccountErased, AccountErasedHandler>(AccountErased.Name);
        return services;
    }
}
