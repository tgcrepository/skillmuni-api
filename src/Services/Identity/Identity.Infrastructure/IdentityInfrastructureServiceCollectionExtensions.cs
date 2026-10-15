using Identity.Application;
using Identity.Application.Abstractions;
using Identity.Application.Events;
using Identity.Application.Handlers;
using Identity.Infrastructure.Notifications;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Messaging.Publishing;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityInfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Identity";

    /// <summary>MySQL (Oracle EF Core provider), repositories, crypto, Google, notifications and the outbox.</summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        var allowsFixedCode = RandomCodeGenerator.AllowsFixedCode(environment);
        services.AddOptions<SecurityOptions>()
            .BindConfiguration(SecurityOptions.Section)
            .Validate(o => !string.IsNullOrWhiteSpace(o.HashKey), "Identity:Security:HashKey is required.")
            .Validate(o => string.IsNullOrEmpty(o.DevelopmentFixedCode) || allowsFixedCode, "Identity:Security:DevelopmentFixedCode is allowed only in Development and Testing.")
            .ValidateOnStart();
        services.AddOptions<TokenSigningOptions>()
            .BindConfiguration("Auth")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningPrivateKeyPem), "Auth:SigningPrivateKeyPem is required.")
            .ValidateOnStart();
        services.AddOptions<ServiceBusOptions>()
            .Validate(
                o => allowsFixedCode || !string.IsNullOrWhiteSpace(o.ConnectionString) || !string.IsNullOrWhiteSpace(o.FullyQualifiedNamespace),
                "ServiceBus must be configured outside Development and Testing: login codes are sent through it.")
            .ValidateOnStart();

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
            options.UseMySQL(connectionString, mysql => mysql
                .MigrationsHistoryTable(IdentityDatabase.HistoryTable)
                .EnableRetryOnFailure(3));

            // The first migration was written without the SDK; CI checks the snapshot instead of failing start-up.
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
        services.AddScoped<IClaimInviteRepository, ClaimInviteRepository>();

        services.AddSingleton<ISecretHasher, HmacSecretHasher>();
        services.AddSingleton<ICodeGenerator, RandomCodeGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        services.AddSkillMuniMessaging<IdentityDbContext>();

        // Events in (inbox + the "identity" subscription): only the Learner photo for AuthUser.photoUrl.
        services.AddSkillMuniEventConsumer<IdentityDbContext>();
        services.AddIntegrationEventHandler<PhotoChanged, PhotoChangedHandler>(PhotoChanged.Name);
        services.AddSingleton<INotificationSender>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClientProvider>().Client;
            if (client is null)
            {
                return new LoggingNotificationSender(sp.GetRequiredService<ILogger<LoggingNotificationSender>>());
            }

            var queue = sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value.NotificationsQueue;
            return new ServiceBusNotificationSender(client.CreateSender(queue));
        });

        return services;
    }
}
