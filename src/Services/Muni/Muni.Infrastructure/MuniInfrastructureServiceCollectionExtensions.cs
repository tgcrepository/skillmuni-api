using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Muni.Application;
using Muni.Application.Handlers;
using Muni.Infrastructure.External;
using Muni.Infrastructure.Persistence;
using Muni.Infrastructure.Worker;

namespace Microsoft.Extensions.DependencyInjection;

public static class MuniInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Muni";
    public const string LearnerBaseAddressKey = "Muni:LearnerBaseAddress";

    /// <summary>MySQL, repositories, the Learner clients, the retention worker and the event consumer.</summary>
    public static IServiceCollection AddMuniInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddDbContext<MuniDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(MuniDatabase.HistoryTable)
                .EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MuniDbContext>());
        services.AddScoped<IMuniLearnerRepository, MuniLearnerRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IMuniDataEraser, MuniDataEraser>();
        services.AddScoped<IChatRetention, ChatRetention>();

        // Learner owns profile progress and private details: typed clients with retry, timeout and circuit breaker.
        // Development and tests without a Learner address use the mock's values.
        var learner = configuration[LearnerBaseAddressKey];
        var isLocal = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (isLocal && string.IsNullOrWhiteSpace(learner))
        {
            services.AddSingleton<IProfileProgressClient, SandboxProfileProgressClient>();
            services.AddSingleton<IPrivateFieldClient, SandboxPrivateFieldClient>();
        }
        else
        {
            var baseAddress = new Uri(string.IsNullOrWhiteSpace(learner) ? "http://learner:8080/" : learner);
            services.AddTransient<ForwardSessionHandler>();
            services.AddHttpClient<IProfileProgressClient, LearnerProfileProgressClient>(c => c.BaseAddress = baseAddress)
                .AddStandardResilienceHandler();
            services.AddHttpClient<IPrivateFieldClient, LearnerPrivateFieldClient>(c => c.BaseAddress = baseAddress)
                .AddHttpMessageHandler<ForwardSessionHandler>()
                .AddStandardResilienceHandler();
        }

        if (configuration.GetValue("Muni:Cleanup:Enabled", true))
        {
            services.AddHostedService<HistoryCleanupWorker>();
        }

        services.AddSkillMuniMessaging<MuniDbContext>();
        services.AddSkillMuniEventConsumer<MuniDbContext>();
        services.AddIntegrationEventHandler<LearnerRegistered, LearnerRegisteredHandler>(LearnerRegistered.Name);
        services.AddIntegrationEventHandler<LearnerErased, LearnerErasedHandler>(LearnerErased.Name);
        services.AddIntegrationEventHandler<XpAwarded, XpAwardedHandler>(XpAwarded.Name);
        services.AddIntegrationEventHandler<QuestCompleted, QuestCompletedHandler>(QuestCompleted.Name);
        services.AddIntegrationEventHandler<AssessmentSubmitted, AssessmentSubmittedHandler>(AssessmentSubmitted.Name);
        return services;
    }

    /// <summary>Every event type the "muni" subscription receives (kept with the handler list).</summary>
    public static readonly IReadOnlyList<string> SubscribedEvents =
    [
        LearnerRegistered.Name, LearnerErased.Name, XpAwarded.Name, QuestCompleted.Name, AssessmentSubmitted.Name,
    ];
}
