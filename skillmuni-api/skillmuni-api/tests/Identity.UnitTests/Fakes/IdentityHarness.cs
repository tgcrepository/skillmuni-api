using Identity.Application;
using Identity.Application.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Identity.UnitTests.Fakes;

/// <summary>Wires the real Application services to in-memory fakes and a controllable clock.</summary>
public sealed class IdentityHarness
{
    public IdentityHarness(string market = "IN")
    {
        Tenant = new FixedTenant(market);
        var options = Options.Create(Settings);
        Codes = new CodeService(Store, Generator, Hasher, Store, Tenant, Clock, options);
        Sessions = new SessionService(Store, Store, Tokens, Generator, Hasher, CurrentUser, Store, Clock, options);
        Login = new LoginService(Store, Store, Codes, Sessions, Passwords, Generator, Hasher, Notifications, Store, Tenant, Clock, options);
        Registration = new RegistrationService(Store, Store, Store, Codes, Sessions, Passwords, Notifications, Store, Store, Tenant, Clock, options);
        Google = new GoogleSignInService(GoogleValidator, TenantSettings, Store, Sessions, Store, Tenant, Clock);
        Contacts = new ContactService(Store, Codes, Notifications, Store, Store, Tenant, Clock);
        Claims = new ClaimService(Store, Store, Store, Codes, Sessions, Generator, Hasher, Notifications, Store, Store, Tenant, Clock, options);
        Consents = new ConsentService(Store, Store, Store, Store, Tenant, Clock, options);
        Privacy = new PrivacyService(Store, Sessions, Store, Store, Clock);
    }

    public IdentityServiceOptions Settings { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));

    public InMemoryStore Store { get; } = new();

    public FixedTenant Tenant { get; }

    public FakeHasher Hasher { get; } = new();

    public FakeCodeGenerator Generator { get; } = new();

    public FakePasswordHasher Passwords { get; } = new();

    public FakeTokenIssuer Tokens { get; } = new();

    public FakeNotifications Notifications { get; } = new();

    public FakeGoogle GoogleValidator { get; } = new();

    public FakeCurrentUser CurrentUser { get; } = new();

    public FakeTenantSettings TenantSettings { get; } = new();

    public CodeService Codes { get; }

    public SessionService Sessions { get; }

    public LoginService Login { get; }

    public RegistrationService Registration { get; }

    public GoogleSignInService Google { get; }

    public ContactService Contacts { get; }

    public ClaimService Claims { get; }

    public ConsentService Consents { get; }

    public PrivacyService Privacy { get; }

    public DateTime Now => Clock.GetUtcNow().UtcDateTime;
}
