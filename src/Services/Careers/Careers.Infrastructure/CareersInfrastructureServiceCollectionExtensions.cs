using Careers.Application;
using Careers.Application.Abstractions;
using Careers.Application.Catalogue;
using Careers.Application.Handlers;
using Careers.Infrastructure.Catalogue;
using Careers.Infrastructure.External;
using Careers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class CareersInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Careers";
    public const string LearnerBaseAddressKey = "Careers:LearnerBaseAddress";

    /// <summary>Every event type the "careers" subscription receives (kept with the handler list).</summary>
    public static readonly IReadOnlyList<string> SubscribedEvents =
    [
        AssessmentSubmitted.Name, JobProfileCompleted.Name, ClaimConfirmed.Name, AccountErased.Name,
    ];

    /// <summary>MySQL, repositories, the seed catalogue, Blob Storage, the Learner client, outbox and event consumer.</summary>
    public static IServiceCollection AddCareersInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddDbContext<CareersDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(CareersDatabase.HistoryTable)
                .EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CareersDbContext>());
        services.AddScoped<ICareerLearnerRepository, CareerLearnerRepository>();
        services.AddScoped<ISavedJobRepository, SavedJobRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<ICvFileRepository, CvFileRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IFestBookingRepository, FestBookingRepository>();
        services.AddScoped<IFestActivityRepository, FestActivityRepository>();
        services.AddScoped<IMatchStateRepository, MatchStateRepository>();
        services.AddScoped<ICareersDataEraser, CareersDataEraser>();

        // Catalogue: seed data until an employer portal / CMS exists (built once, read-only).
        services.AddSingleton<IJobCatalogue, SeedJobCatalogue>();
        services.AddSingleton<IFestCatalogue, SeedFestCatalogue>();
        services.AddSingleton<ICareerCatalogue, SeedCareerCatalogue>();

        services.AddSkillMuniBlobStorage(configuration, environment, "Careers:Storage", "careers-files");

        // Learner (internal ingress): short timeouts so a slow Learner degrades one card, not the page.
        var learner = configuration[LearnerBaseAddressKey];
        services.AddHttpClient<ILearnerDirectory, LearnerDirectory>(c => c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(learner) ? "http://learner:8080/" : learner))
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 2;
                o.Retry.Delay = TimeSpan.FromMilliseconds(200);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(6);
            });

        services.AddSkillMuniMessaging<CareersDbContext>();
        services.AddSkillMuniEventConsumer<CareersDbContext>();
        services.AddIntegrationEventHandler<AssessmentSubmitted, AssessmentSubmittedHandler>(AssessmentSubmitted.Name);
        services.AddIntegrationEventHandler<JobProfileCompleted, JobProfileCompletedHandler>(JobProfileCompleted.Name);
        services.AddIntegrationEventHandler<ClaimConfirmed, ClaimConfirmedHandler>(ClaimConfirmed.Name);
        services.AddIntegrationEventHandler<AccountErased, AccountErasedHandler>(AccountErased.Name);
        return services;
    }
}
