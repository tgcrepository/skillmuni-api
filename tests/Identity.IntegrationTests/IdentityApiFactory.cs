using System.Security.Cryptography;
using System.Text.Json;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillMuni.Messaging.Inbox;
using Testcontainers.MySql;

namespace Identity.IntegrationTests;

/// <summary>The real Identity API against MySQL 8.4 in a container, with only Google's token check faked.</summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string FixedCode = "123456";

    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("identity")
        .WithUsername("identity")
        .WithPassword("identity-test")
        .Build();

    private readonly RSA _signingKey = RSA.Create(2048);

    public FakeGoogle Google { get; } = new();

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _signingKey.Dispose();
    }

    public HttpClient CreateLearnerClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        // Session cookies are Secure, so the client must talk "https".
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    public async Task SeedAsync(Func<IdentityDbContext, IServiceProvider, Task> seed)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await seed(db, scope.ServiceProvider);
        await db.SaveChangesAsync();
    }

    /// <summary>Delivers an event as the Service Bus consumer would (inbox, tenant, one transaction).</summary>
    public Task<DispatchOutcome> DeliverAsync(string eventType, object payload, Guid? eventId = null) =>
        Services.GetRequiredService<IntegrationEventDispatcher<IdentityDbContext>>()
            .DispatchAsync(eventType, eventId ?? Guid.NewGuid(), "skillmuni-in", JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    public async Task<T> QueryAsync<T>(Func<IdentityDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Identity", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPrivateKeyPem", _signingKey.ExportRSAPrivateKeyPem());
        builder.UseSetting("Auth:SigningPublicKeyPem", _signingKey.ExportSubjectPublicKeyInfoPem());
        builder.UseSetting("Identity:Security:HashKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Identity:Security:DevelopmentFixedCode", FixedCode);
        builder.UseSetting("Tenancy:Tenants:skillmuni-in:GoogleClientId", "test-client");
        builder.UseSetting("Outbox:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator>(Google);
        });
    }
}

public sealed class FakeGoogle : IGoogleTokenValidator
{
    public Task<GoogleIdentity?> ValidateAsync(string credential, string clientId, CancellationToken cancellationToken)
    {
        // "google:<subject>:<email>:<first>" is a valid token; anything else is rejected.
        var parts = credential.Split(':');
        return Task.FromResult<GoogleIdentity?>(parts is ["google", var subject, var email, var first] && clientId == "test-client"
            ? new GoogleIdentity(subject, email, true, first, "Test")
            : null);
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "identity-api";
}
