using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Application;
using Platform.Application.Handlers;
using Platform.Application.Services;
using Platform.Domain;
using SkillMuni.Common.Paging;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Platform.UnitTests;

public sealed class PlatformTests
{
    private static readonly Guid Learner = Guid.Parse("0f8d2c4a-1b3e-4a5f-8c7d-9e0a1b2c3d4e");

    private readonly PlatformStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 15, 6, 0, 0, TimeSpan.Zero));
    private readonly RecordingSender _sms = new("sms");
    private readonly RecordingSender _email = new("email");

    private GeoService Geo => new(_store);

    private TenantConfigService Config => new(_store, _store, MsOptions.Create(new PlatformOptions()));

    private NotificationService Notifications => new(_store, _store, _store, _store, _clock);

    private NotificationDispatcher Dispatcher => new([_sms, _email], _store, _store, _clock, NullLogger<NotificationDispatcher>.Instance);

    [Fact]
    public async Task Postal_lookup_gives_areas_under_the_district()
    {
        var hit = (await Geo.LookupAsync("in", " 560 038 ", CancellationToken.None)).Value;

        Assert.Equal("Karnataka", hit.Region);
        Assert.Equal("Bengaluru", hit.City);
        Assert.Equal(new PostalAreaDto("Indiranagar", "Bengaluru Urban"), hit.Areas[0]);
    }

    [Fact]
    public async Task Unknown_postal_code_is_404()
    {
        Assert.Equal(PlatformErrors.PostalNotFound, (await Geo.LookupAsync("IN", "999999", CancellationToken.None)).Error);
        Assert.Equal(PlatformErrors.PostalNotFound, (await Geo.LookupAsync("IN", "   ", CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Regions_for_an_unknown_market_are_empty()
    {
        Assert.Equal(2, (await Geo.RegionsAsync("za", CancellationToken.None)).Count);
        Assert.Empty(await Geo.RegionsAsync("US", CancellationToken.None));
    }

    [Fact]
    public async Task Tenant_without_a_theme_gets_skillmuni()
    {
        var theme = await Config.ThemeAsync(CancellationToken.None);

        Assert.Equal(new TenantThemeDto("skillmuni-in", "Skillmuni", null, null, null), theme);
    }

    [Fact]
    public async Task Theme_ramps_keep_only_known_steps_and_hex_colours()
    {
        _store.Themes.Add(TenantTheme.Create("skillmuni-in", "Acme Skills", "https://cdn.example/acme.svg", """{"500":"#FF6A00","999":"#000000","400":"orange"}""", "not json", _clock.GetUtcNow().UtcDateTime));

        var theme = await Config.ThemeAsync(CancellationToken.None);

        Assert.Equal("Acme Skills", theme.Name);
        Assert.Equal("#FF6A00", Assert.Single(theme.Brand!).Value);
        Assert.Null(theme.Accent);
        Assert.Equal("https://cdn.example/acme.svg", theme.LogoUrl);
    }

    [Fact]
    public void Market_config_follows_the_tenant_or_the_code()
    {
        var india = Config.Market(null).Value;
        Assert.Equal("+91", india.DialCode);
        Assert.Equal("Asia/Kolkata", india.TimeZone);
        Assert.Equal("digilocker", india.IdVerification);
        Assert.Equal(@"^[6-9]\d{9}$", india.Mobile.Pattern);

        var southAfrica = Config.Market("za").Value;
        Assert.Equal("Africa/Johannesburg", southAfrica.TimeZone);
        Assert.Equal("luhn", southAfrica.IdTypes[0].Check);
        Assert.True(southAfrica.AsksRace);

        Assert.Equal(PlatformErrors.UnknownMarket, Config.Market("US").Error);
    }

    [Fact]
    public async Task Badges_notify_once_and_can_be_read()
    {
        var handler = new BadgeEarnedHandler(Notifications);
        var earned = new BadgeEarned(Learner, "quest-who-dis", "ID Unlocked") { TenantId = "skillmuni-in" };
        await handler.HandleAsync(earned, CancellationToken.None);
        await handler.HandleAsync(earned, CancellationToken.None);

        var page = await Notifications.ListAsync(Learner, PageRequest.Create(1, 20), CancellationToken.None);
        var item = Assert.Single(page.Items);
        Assert.Equal("Badge unlocked: ID Unlocked", item.Title);
        Assert.Equal("/stats/badges", item.Link);
        Assert.False(item.Read);
        Assert.Equal(1, page.Unread);

        Assert.True((await Notifications.MarkReadAsync(Learner, Guid.Parse(item.Id), CancellationToken.None)).IsSuccess);
        Assert.Equal(0, (await Notifications.ListAsync(Learner, PageRequest.Create(1, 20), CancellationToken.None)).Unread);
        Assert.Equal(PlatformErrors.NotificationNotFound, (await Notifications.MarkReadAsync(Learner, Guid.NewGuid(), CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Preferences_default_then_save()
    {
        Assert.Equal(new NotificationPreferencesDto(true, false, false, true), await Notifications.PreferencesAsync(Learner, CancellationToken.None));
        Assert.Empty(_store.Preferences);

        var saved = await Notifications.SavePreferencesAsync(Learner, new UpdatePreferencesRequest(false, true, true, false), CancellationToken.None);

        Assert.Equal(new NotificationPreferencesDto(false, true, true, false), saved);
        Assert.Single(_store.Preferences);
    }

    [Fact]
    public void Preferences_need_every_channel()
    {
        var result = new UpdatePreferencesRequestValidator().Validate(new UpdatePreferencesRequest(true, null, true, true));
        Assert.Equal("WhatsApp", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Identity_queue_message_is_read()
    {
        var message = QueuedNotification.TryParse("""{"tenantId":"skillmuni-in","channel":"sms","to":"+919876543210","template":"login-code","data":{"secret":"123456","validForMinutes":10}}""");

        Assert.NotNull(message);
        Assert.Equal("sms", message.Channel);
        Assert.Equal("123456", message.Data!.Secret);
        Assert.Null(QueuedNotification.TryParse("not json"));
        Assert.Null(QueuedNotification.TryParse("""{"channel":"sms","to":"+919876543210","template":"login-code"}"""));
    }

    [Fact]
    public async Task Dispatch_sends_once_and_records_a_masked_delivery()
    {
        var message = new QueuedNotification("skillmuni-in", "SMS", "+919876543210", "login-code", new QueuedNotificationData("123456", 10));

        Assert.Equal(DispatchResult.Sent, await Dispatcher.DispatchAsync(message, "m-1", CancellationToken.None));
        Assert.Equal(DispatchResult.Duplicate, await Dispatcher.DispatchAsync(message, "m-1", CancellationToken.None));

        var sent = Assert.Single(_sms.Sent);
        Assert.Equal("123456", sent.Secret);
        var delivery = Assert.Single(_store.Deliveries);
        Assert.Equal("***3210", delivery.Recipient);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal("recording", delivery.Provider);
        Assert.Equal("m-1", delivery.QueueMessageId);
    }

    [Fact]
    public async Task Dispatch_without_a_sender_is_skipped()
    {
        var message = new QueuedNotification("skillmuni-in", "pigeon", "yash.p@gmail.com", "login-code", null);

        Assert.Equal(DispatchResult.Skipped, await Dispatcher.DispatchAsync(message, null, CancellationToken.None));

        var delivery = Assert.Single(_store.Deliveries);
        Assert.Equal(DeliveryStatus.Skipped, delivery.Status);
        Assert.Equal("y***@gmail.com", delivery.Recipient);
        Assert.Empty(_email.Sent);
    }
}
