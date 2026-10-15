using System.Text.Json;
using Assessment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Assessment.IntegrationTests;

/// <summary>The real Assessment API against MySQL 8.4 in a container. The outbox is left unsent so tests can read it.</summary>
public sealed class AssessmentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("assessment")
        .WithUsername("assessment")
        .WithPassword("assessment-test")
        .Build();

    private readonly TestTokens _tokens = new();

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _tokens.Dispose();
    }

    public HttpClient CreateLearnerClient(out Guid learnerId, string market = "IN")
    {
        learnerId = Guid.NewGuid();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _tokens.Create(learnerId, market: market));
        return client;
    }

    public Task<DispatchOutcome> DeliverAsync(string eventType, object payload) =>
        Services.GetRequiredService<IntegrationEventDispatcher<AssessmentDbContext>>()
            .DispatchAsync(eventType, Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    public async Task<T> QueryAsync<T>(Func<AssessmentDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<AssessmentDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Assessment", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Outbox:Enabled", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class AssessmentApiCollection : ICollectionFixture<AssessmentApiFactory>
{
    public const string Name = "assessment-api";
}
