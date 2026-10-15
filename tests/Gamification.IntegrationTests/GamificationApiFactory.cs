using System.Text.Json;
using Gamification.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Gamification.IntegrationTests;

/// <summary>The real Gamification API against MySQL 8.4; events are delivered through the real dispatcher.</summary>
public sealed class GamificationApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("gamification")
        .WithUsername("gamification")
        .WithPassword("gamification-test")
        .Build();

    private readonly TestTokens _tokens = new();

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _tokens.Dispose();
    }

    public HttpClient ClientFor(Guid learnerId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _tokens.Create(learnerId));
        return client;
    }

    public Task<DispatchOutcome> DeliverAsync(string eventType, object payload, Guid? eventId = null) =>
        Services.GetRequiredService<IntegrationEventDispatcher<GamificationDbContext>>()
            .DispatchAsync(eventType, eventId ?? Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    /// <summary>A registered learner, as Identity's event creates them.</summary>
    public async Task<Guid> JoinAsync(string first, string last)
    {
        var id = Guid.NewGuid();
        await DeliverAsync("identity.learner.registered", new { accountId = id, firstName = first, lastName = last, market = "IN", tenantId = TestTokens.Tenant });
        return id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Gamification", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Gamification:Arena:CacheFor", "00:00:00.001");
        builder.UseSetting("Outbox:Enabled", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class GamificationApiCollection : ICollectionFixture<GamificationApiFactory>
{
    public const string Name = "gamification-api";
}
