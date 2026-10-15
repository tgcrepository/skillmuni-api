using System.Text.Json;
using Learning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Learning.IntegrationTests;

/// <summary>The real Learning API against MySQL 8.4; events are delivered through the real dispatcher.</summary>
public sealed class LearningApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("learning")
        .WithUsername("learning")
        .WithPassword("learning-test")
        .Build();

    private readonly TestTokens _tokens = new();

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _tokens.Dispose();
    }

    /// <summary>A client signed in as a brand-new learner.</summary>
    public HttpClient ClientFor(out Guid learnerId)
    {
        learnerId = Guid.NewGuid();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _tokens.Create(learnerId));
        return client;
    }

    public Task<DispatchOutcome> DeliverAsync(string eventType, object payload, Guid? eventId = null) =>
        Services.GetRequiredService<IntegrationEventDispatcher<LearningDbContext>>()
            .DispatchAsync(eventType, eventId ?? Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    public async Task<T> QueryAsync<T>(Func<LearningDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<LearningDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Learning", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Outbox:Enabled", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class LearningApiCollection : ICollectionFixture<LearningApiFactory>
{
    public const string Name = "learning-api";
}
