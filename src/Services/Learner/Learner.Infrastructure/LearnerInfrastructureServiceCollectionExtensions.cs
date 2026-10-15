using Learner.Application.Abstractions;
using Learner.Application.Events;
using Learner.Application.Handlers;
using Learner.Infrastructure.External;
using Learner.Infrastructure.Persistence;
using Learner.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class LearnerInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Learner";
    /// <summary>MySQL, repositories, Blob Storage, field encryption, outside APIs, cache, outbox and event consumer.</summary>
    public static IServiceCollection AddLearnerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var isLocal = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        services.AddDbContext<LearnerDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(LearnerDatabase.HistoryTable)
                .EnableRetryOnFailure(3));

            // The first migration was generated without the SDK; CI checks the snapshot instead of failing start-up.
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LearnerDbContext>());
        services.AddScoped<ILearnerRepository, LearnerRepository>();
        services.AddScoped<IQuestAnswersRepository, QuestAnswersRepository>();
        services.AddScoped<IDraftRepository, DraftRepository>();
        services.AddScoped<ILinkedIdentityRepository, LinkedIdentityRepository>();
        services.AddScoped<IProgrammeRepository, ProgrammeRepository>();
        services.AddScoped<IEnrolmentRepository, EnrolmentRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<ITrophyFileRepository, TrophyFileRepository>();
        services.AddScoped<IJobProfileRepository, JobProfileRepository>();
        services.AddScoped<ILearnerDataEraser, LearnerDataEraser>();

        // Field encryption: the key is a Key Vault secret (Learner--Security--EncryptionKey).
        services.AddOptions<LearnerSecurityOptions>()
            .BindConfiguration(LearnerSecurityOptions.Section)
            .Validate(o => LearnerSecurityOptions.IsValidKey(o.EncryptionKey), "Learner:Security:EncryptionKey must be 32 random bytes, base64.")
            .ValidateOnStart();
        services.AddSingleton<IFieldEncryptor, AesGcmFieldEncryptor>();

        // Files: Blob Storage with managed identity in Azure; in memory for local runs without Azurite.
        services.AddSkillMuniBlobStorage(configuration, environment, "Learner:Storage", "learner-files");

        // IDistributedCache (Redis in Azure) comes from AddSkillMuniServiceDefaults.

        // Outside APIs: typed clients with retry, timeout and circuit breaker (CLAUDE.md §9).
        services.AddHttpClient<IBankDirectory, RazorpayBankDirectory>(c => c.BaseAddress = new Uri(RazorpayBankDirectory.BaseAddress))
            .AddStandardResilienceHandler();
        services.AddHttpClient<IEscoClient, EscoClient>(c => c.BaseAddress = new Uri(EscoClient.BaseAddress))
            .AddStandardResilienceHandler();

        // The link check must answer within 5 s, so no retries; its handler blocks private addresses (SSRF).
        services.AddHttpClient<ILinkChecker, PublicLinkChecker>(c =>
            {
                c.Timeout = PublicLinkChecker.Timeout + TimeSpan.FromSeconds(1);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("SkillMuni-LinkCheck/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(PublicLinkChecker.CreateHandler);

        services.AddOptions<DigiLockerOptions>()
            .BindConfiguration(DigiLockerOptions.Section)
            .Validate(o => o.Mode != DigiLockerOptions.Sandbox || isLocal, "Learner:DigiLocker:Mode=sandbox is allowed only in Development and Testing.")
            .ValidateOnStart();
        var digiLocker = configuration.GetSection(DigiLockerOptions.Section).Get<DigiLockerOptions>() ?? new DigiLockerOptions();
        if (digiLocker.Mode == DigiLockerOptions.Sandbox && isLocal)
        {
            services.AddSingleton<IDigiLockerClient, SandboxDigiLockerClient>();
        }
        else
        {
            services.AddSingleton<IDigiLockerClient, UnavailableDigiLockerClient>();
        }

        // Events out (outbox) and in (inbox + the "learner" subscription).
        services.AddSkillMuniMessaging<LearnerDbContext>();
        services.AddSkillMuniEventConsumer<LearnerDbContext>();
        services.AddIntegrationEventHandler<AccountRegistered, AccountRegisteredHandler>(AccountRegistered.Name);
        services.AddIntegrationEventHandler<ContactVerified, ContactVerifiedHandler>(ContactVerified.Name);
        services.AddIntegrationEventHandler<ClaimConfirmed, ClaimConfirmedHandler>(ClaimConfirmed.Name);
        services.AddIntegrationEventHandler<AccountErased, AccountErasedHandler>(AccountErased.Name);
        services.AddIntegrationEventHandler<XpAwarded, XpAwardedHandler>(XpAwarded.Name);
        services.AddIntegrationEventHandler<AssessmentSubmitted, AssessmentSubmittedHandler>(AssessmentSubmitted.Name);
        services.AddIntegrationEventHandler<CvParsed, CvParsedHandler>(CvParsed.Name);

        return services;
    }
}
