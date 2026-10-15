using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Services;
using Platform.Infrastructure.Notifications;
using Platform.Infrastructure.Persistence;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Platform.IntegrationTests;

[Collection(PlatformApiCollection.Name)]
public sealed class PlatformEndpointsTests(PlatformApiFactory api)
{
    [Fact]
    public async Task Postal_lookup_matches_the_mock()
    {
        var client = api.CreateClient();

        var hit = await (await client.GetAsync("/api/v1/geo/IN/postal/560038")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Karnataka", hit.GetProperty("region").GetString());
        Assert.Equal("Bengaluru Urban", hit.GetProperty("district").GetString());
        Assert.Equal("Bengaluru", hit.GetProperty("city").GetString());
        Assert.Equal(7, hit.GetProperty("areas").GetArrayLength());
        Assert.Equal("Indiranagar", hit.GetProperty("areas")[0].GetProperty("name").GetString());
        Assert.Equal("Bengaluru Urban", hit.GetProperty("areas")[0].GetProperty("sub").GetString());

        var za = await (await client.GetAsync("/api/v1/geo/ZA/postal/2001")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(new string?[] { "Braamfontein", "Marshalltown" }, za.GetProperty("areas").EnumerateArray().Select(a => a.GetProperty("name").GetString()));

        Assert.Equal("postal-not-found", await (await client.GetAsync("/api/v1/geo/IN/postal/999999")).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Regions_per_market()
    {
        var client = api.CreateClient();

        Assert.Equal(31, (await (await client.GetAsync("/api/v1/geo/IN/regions")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());
        var za = await (await client.GetAsync("/api/v1/geo/ZA/regions")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Eastern Cape", za[0].GetString());
        Assert.Equal(9, za.GetArrayLength());
        Assert.Equal(0, (await (await client.GetAsync("/api/v1/geo/US/regions")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());
    }

    [Fact]
    public async Task Theme_and_market_follow_the_tenant_header()
    {
        var client = api.CreateClient();

        var theme = await (await client.GetAsync("/api/v1/tenant/theme")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("skillmuni-in", theme.GetProperty("id").GetString());
        Assert.Equal("Skillmuni", theme.GetProperty("name").GetString());
        Assert.False(theme.TryGetProperty("brand", out _));
        Assert.False(theme.TryGetProperty("logoUrl", out _));

        using var za = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenant/theme");
        za.Headers.Add("X-Tenant-Id", "skillmuni-za");
        Assert.Equal("skillmuni-za", (await (await client.SendAsync(za)).JsonAsync(HttpStatusCode.OK)).GetProperty("id").GetString());

        using var zaMarket = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenant/market");
        zaMarket.Headers.Add("X-Tenant-Id", "skillmuni-za");
        var market = await (await client.SendAsync(zaMarket)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("ZA", market.GetProperty("code").GetString());
        Assert.Equal("+27", market.GetProperty("dialCode").GetString());
        Assert.Equal("Africa/Johannesburg", market.GetProperty("timeZone").GetString());
        Assert.Equal("^\\d{4}$", market.GetProperty("postal").GetProperty("pattern").GetString());

        var india = await (await client.GetAsync("/api/v1/tenant/market")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("IN", india.GetProperty("code").GetString());
        Assert.Equal("PIN code", india.GetProperty("postal").GetProperty("label").GetString());
        Assert.Equal(3, india.GetProperty("idTypes").GetArrayLength());
        Assert.False(india.GetProperty("idTypes")[0].TryGetProperty("check", out _));

        Assert.Equal("unknown-market", await (await client.GetAsync("/api/v1/tenant/market?code=US")).ProblemCodeAsync(HttpStatusCode.NotFound));

        using var unknown = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenant/theme");
        unknown.Headers.Add("X-Tenant-Id", "nobody");
        Assert.Equal("unknown-tenant", await (await client.SendAsync(unknown)).ProblemCodeAsync(HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Badge_events_fill_the_bell()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);
        Assert.Equal(0, (await (await client.GetAsync("/api/v1/me/notifications")).JsonAsync(HttpStatusCode.OK)).GetProperty("total").GetInt32());

        var earned = new { learnerId = me, badgeId = "quest-who-dis", badgeName = "ID Unlocked", tenantId = TestTokens.Tenant };
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("gamification.badge.earned", earned));
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("gamification.badge.earned", earned));

        var page = await (await client.GetAsync("/api/v1/me/notifications?page=1&pageSize=10")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("unread").GetInt32());
        Assert.Equal(10, page.GetProperty("pageSize").GetInt32());
        var item = page.GetProperty("items")[0];
        Assert.Equal("badge", item.GetProperty("kind").GetString());
        Assert.False(item.GetProperty("read").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"/api/v1/me/notifications/{item.GetProperty("id").GetString()}/read", new { })).StatusCode);
        Assert.Equal(0, (await (await client.GetAsync("/api/v1/me/notifications")).JsonAsync(HttpStatusCode.OK)).GetProperty("unread").GetInt32());
        Assert.Equal("notification-not-found", await (await client.PutJsonAsync($"/api/v1/me/notifications/{Guid.NewGuid()}/read", new { })).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Preferences_round_trip_and_validate()
    {
        var client = api.ClientFor(Guid.NewGuid());

        var defaults = await (await client.GetAsync("/api/v1/me/notification-preferences")).JsonAsync(HttpStatusCode.OK);
        Assert.True(defaults.GetProperty("push").GetBoolean());
        Assert.False(defaults.GetProperty("whatsApp").GetBoolean());

        var saved = await (await client.PutJsonAsync("/api/v1/me/notification-preferences", new { push = false, whatsApp = true, sms = true, email = false })).JsonAsync(HttpStatusCode.OK);
        Assert.True(saved.GetProperty("whatsApp").GetBoolean());
        Assert.False((await (await client.GetAsync("/api/v1/me/notification-preferences")).JsonAsync(HttpStatusCode.OK)).GetProperty("push").GetBoolean());

        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/me/notification-preferences", new { push = true })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Queued_codes_are_sent_through_the_sandbox_and_recorded_masked()
    {
        var consumer = api.Services.GetRequiredService<NotificationQueueConsumer>();
        var messageId = Guid.NewGuid().ToString();
        var body = JsonSerializer.Serialize(
            new { tenantId = TestTokens.Tenant, channel = "sms", to = "+919876543210", template = "login-code", data = new { secret = "482913", validForMinutes = 10 } },
            JsonSerializerOptions.Web);

        Assert.Equal(DispatchResult.Sent, await consumer.HandleAsync(body, messageId, CancellationToken.None));
        Assert.Equal(DispatchResult.Duplicate, await consumer.HandleAsync(body, messageId, CancellationToken.None));
        Assert.Null(await consumer.HandleAsync("{}", Guid.NewGuid().ToString(), CancellationToken.None));

        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var delivery = await db.Deliveries.SingleAsync(d => d.QueueMessageId == messageId);
        Assert.Equal("***3210", delivery.Recipient);
        Assert.Equal("sandbox", delivery.Provider);
        Assert.Equal("sms", delivery.Channel);
        Assert.DoesNotContain("482913", JsonSerializer.Serialize(delivery), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erasure_removes_notifications_and_preferences()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);
        await client.PutJsonAsync("/api/v1/me/notification-preferences", new { push = false, whatsApp = false, sms = false, email = false });
        await api.DeliverAsync("gamification.badge.earned", new { learnerId = me, badgeId = "quest-hit-me-up", badgeName = "Signal Boost", tenantId = TestTokens.Tenant });

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = me, tenantId = TestTokens.Tenant }));

        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.Notifications.AnyAsync(n => n.LearnerId == me));
        Assert.False(await db.Preferences.AnyAsync(p => p.Id == me));
    }

    [Fact]
    public async Task Notifications_need_a_session() =>
        Assert.Equal("no-session", await (await api.CreateClient().GetAsync("/api/v1/me/notifications")).ProblemCodeAsync(HttpStatusCode.Unauthorized));
}
