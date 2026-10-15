using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Persistence;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Platform.IntegrationTests;

/// <summary>The real Platform API against MySQL 8.4; events go through the real dispatcher, queue bodies through the consumer.</summary>
public sealed class PlatformApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("platform")
        .WithUsername("platform")
        .WithPassword("platform-test")
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
        Services.GetRequiredService<IntegrationEventDispatcher<PlatformDbContext>>()
            .DispatchAsync(eventType, eventId ?? Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Platform", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Outbox:Enabled", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class PlatformApiCollection : ICollectionFixture<PlatformApiFactory>
{
    public const string Name = "platform-api";
}
