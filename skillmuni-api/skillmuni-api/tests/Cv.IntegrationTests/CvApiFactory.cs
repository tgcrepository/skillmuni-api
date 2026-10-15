using System.Text.Json;
using Cv.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Cv.IntegrationTests;

/// <summary>The real CV API and parse worker against MySQL 8.4 in a container, reading with the sample-CV sandbox.</summary>
public sealed class CvApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("cv")
        .WithUsername("cv")
        .WithPassword("cv-test")
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
        Services.GetRequiredService<IntegrationEventDispatcher<CvDbContext>>()
            .DispatchAsync(eventType, Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    public async Task<T> QueryAsync<T>(Func<CvDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<CvDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Cv", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Cv:Reader:Mode", "sandbox");
        builder.UseSetting("Cv:IdentityBaseAddress", string.Empty);
        builder.UseSetting("Cv:PollInterval", "00:00:00.200");
        builder.UseSetting("Outbox:Enabled", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class CvApiCollection : ICollectionFixture<CvApiFactory>
{
    public const string Name = "cv-api";
}
