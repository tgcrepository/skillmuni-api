using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Muni.Infrastructure.Persistence;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Muni.IntegrationTests;

/// <summary>
/// The real Muni API against MySQL 8.4; events go through the real dispatcher. Learner isn't running, so the
/// sandbox Learner clients answer (the mock's email and mobile, a half-done profile).
/// </summary>
public sealed class MuniApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("muni")
        .WithUsername("muni")
        .WithPassword("muni-test")
        .Build();

    private readonly TestTokens _tokens = new();

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _tokens.Dispose();
    }

    public HttpClient ClientFor(Guid learnerId, string firstName = "Yash")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _tokens.Create(learnerId, firstName));
        return client;
    }

    public Task<DispatchOutcome> DeliverAsync(string eventType, object payload, Guid? eventId = null) =>
        Services.GetRequiredService<IntegrationEventDispatcher<MuniDbContext>>()
            .DispatchAsync(eventType, eventId ?? Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Muni", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Muni:LearnerBaseAddress", string.Empty);
        builder.UseSetting("Muni:Cleanup:Enabled", "false");
        builder.UseSetting("Outbox:Enabled", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class MuniApiCollection : ICollectionFixture<MuniApiFactory>
{
    public const string Name = "muni-api";
}
