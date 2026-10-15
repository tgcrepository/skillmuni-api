using System.Security.Cryptography;
using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Learner.IntegrationTests;

/// <summary>
/// The real Learner API against MySQL 8.4 in a container. Sessions are minted with a test key (as Identity would);
/// only the outside internet (IFSC, ESCO, link pages) is faked. DigiLocker runs in sandbox mode.
/// </summary>
public sealed class LearnerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Tenant = "skillmuni-in";

    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("learner")
        .WithUsername("learner")
        .WithPassword("learner-test")
        .Build();

    private readonly TestTokens _tokens = new();

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _tokens.Dispose();
    }

    /// <summary>A client signed in as a new learner (bearer token; the cookie path is covered by Identity's tests).</summary>
    public HttpClient CreateLearnerClient(out Guid learnerId, string firstName = "Asha", params string[] roles)
    {
        learnerId = Guid.NewGuid();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(learnerId, firstName, roles.Length > 0 ? roles : ["learner"]));
        return client;
    }

    public HttpClient CreateLearnerClient(string firstName = "Asha") => CreateLearnerClient(out _, firstName);

    public string Token(Guid learnerId, string firstName, string[] roles, string tenant = Tenant) =>
        _tokens.Create(learnerId, firstName, roles, tenant);

    /// <summary>Delivers an event as the Service Bus consumer would (inbox, tenant, one transaction).</summary>
    public Task<DispatchOutcome> DeliverAsync(string eventType, Guid eventId, object payload) =>
        Services.GetRequiredService<IntegrationEventDispatcher<LearnerDbContext>>()
            .DispatchAsync(eventType, eventId, Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    public async Task<T> QueryAsync<T>(Func<LearnerDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<LearnerDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Learner", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Learner:Security:EncryptionKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Learner:DigiLocker:Mode", "sandbox");
        builder.UseSetting("Outbox:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBankDirectory>();
            services.AddSingleton<IBankDirectory, OfflineBankDirectory>();
            services.RemoveAll<IEscoClient>();
            services.AddSingleton<IEscoClient, OfflineEsco>();
            services.RemoveAll<ILinkChecker>();
            services.AddSingleton<ILinkChecker, OfflineLinkChecker>();
        });
    }

    private sealed class OfflineBankDirectory : IBankDirectory
    {
        public Task<Lookup<BankBranchInfo>> FindIfscAsync(string ifsc, CancellationToken cancellationToken) =>
            Task.FromResult(ifsc == "HDFC0000240"
                ? Lookup<BankBranchInfo>.Found(new BankBranchInfo("HDFC Bank", "Andheri West, Mumbai"))
                : Lookup<BankBranchInfo>.NotFound());
    }

    private sealed class OfflineEsco : IEscoClient
    {
        public Task<Lookup<EscoSearchDto>> SearchAsync(string text, string language, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(text == "esco-down"
                ? Lookup<EscoSearchDto>.Unavailable()
                : Lookup<EscoSearchDto>.Found(new EscoSearchDto(1, [new("http://data.europa.eu/esco/skill/x", text, "skill", "people", null, null)])));
    }

    private sealed class OfflineLinkChecker : ILinkChecker
    {
        public Task<Lookup<bool>> PageMentionsAsync(Uri url, IReadOnlyList<string> names, CancellationToken cancellationToken) =>
            Task.FromResult(url.AbsolutePath.Contains("fail", StringComparison.Ordinal)
                ? Lookup<bool>.Timeout()
                : Lookup<bool>.Found(url.AbsolutePath.Contains("verify", StringComparison.Ordinal)));
    }
}

[CollectionDefinition(Name)]
public sealed class LearnerApiCollection : ICollectionFixture<LearnerApiFactory>
{
    public const string Name = "learner-api";
}
