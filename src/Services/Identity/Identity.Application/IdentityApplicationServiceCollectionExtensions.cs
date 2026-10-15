using FluentValidation;
using Identity.Application.Contracts;
using Identity.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityApplicationServiceCollectionExtensions
{
    /// <summary>Use cases and request validators. Options are bound by the host.</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<ICodeService, CodeService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IGoogleSignInService, GoogleSignInService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IClaimService, ClaimService>();
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IAccountLookupService, AccountLookupService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: false);
        return services;
    }
}
