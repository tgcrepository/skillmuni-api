using System.Collections.Concurrent;
using System.Text.Json;
using Careers.Application.Abstractions;
using Careers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;
using Testcontainers.MySql;

namespace Careers.IntegrationTests;

/// <summary>
/// The real Careers API against MySQL 8.4; events go through the real dispatcher. Learner is replaced by
/// <see cref="StubLearners"/> (per-learner answers), blobs live in memory.
/// </summary>
public sealed class CareersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("careers")
        .WithUsername("careers")
        .WithPassword("careers-test")
        .Build();

    private readonly TestTokens _tokens = new();

    /// <summary>15 Oct 2026, 11:30 IST: the seed fests (Dec 2026 – Jan 2027) are open for booking.</summary>
    public static readonly DateTimeOffset Today = new(2026, 10, 15, 6, 0, 0, TimeSpan.Zero);

    public StubLearners Learners { get; } = new();

    /// <summary>The service's clock. A test that moves it puts it back to <see cref="Today"/> when done.</summary>
    public SettableClock Clock { get; } = new(Today);

    public async Task InitializeAsync() => await _mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mysql.DisposeAsync();
        await DisposeAsync();
        _tokens.Dispose();
    }

    public HttpClient ClientFor(Guid learnerId, string market = "IN")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _tokens.Create(learnerId, "Yash", market: market));
        return client;
    }

    public Task<DispatchOutcome> DeliverAsync(string eventType, object payload, Guid? eventId = null) =>
        Services.GetRequiredService<IntegrationEventDispatcher<CareersDbContext>>()
            .DispatchAsync(eventType, eventId ?? Guid.NewGuid(), TestTokens.Tenant, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), CancellationToken.None);

    public async Task<T> QueryAsync<T>(Func<CareersDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<CareersDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Careers", _mysql.GetConnectionString() + ";AllowPublicKeyRetrieval=True");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningPublicKeyPem", _tokens.PublicKeyPem);
        builder.UseSetting("Outbox:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<ILearnerDirectory>();
            services.AddSingleton<ILearnerDirectory>(Learners);
        });
    }
}

/// <summary>A clock tests can set forwards and back (the fake from TimeProvider.Testing only moves forwards).</summary>
public sealed class SettableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void SetUtcNow(DateTimeOffset value) => _now = value;
}

/// <summary>What Learner answers for each learner (unknown learners: no profile yet, skills unknown).</summary>
public sealed class StubLearners : ILearnerDirectory
{
    private readonly ConcurrentDictionary<Guid, int> _percent = new();
    private readonly ConcurrentDictionary<Guid, LearnerCareerProfile> _profiles = new();

    public void SetPercent(Guid learnerId, int percent) => _percent[learnerId] = percent;

    public void SetProfile(Guid learnerId, LearnerCareerProfile profile) => _profiles[learnerId] = profile;

    public Task<ProgressLookup> ProgressAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(_percent.TryGetValue(learnerId, out var percent)
            ? new ProgressLookup(LookupStatus.Found, new ProfileProgress(percent, percent * 18 / 100, 18, [new QuestFill(4, "your-flex-era", "Your Flex Era", 2, 3, [new QuestStepGap("trophies", "Trophies", 3)])]))
            : ProgressLookup.Missing);

    public Task<LearnerCareerProfile?> CareerProfileAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.TryGetValue(learnerId, out var profile) ? profile : null);
}

[CollectionDefinition(Name)]
public sealed class CareersApiCollection : ICollectionFixture<CareersApiFactory>
{
    public const string Name = "careers-api";
}
