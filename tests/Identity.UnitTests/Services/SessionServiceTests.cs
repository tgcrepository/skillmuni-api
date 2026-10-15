using Identity.Domain.Accounts;
using Identity.UnitTests.Fakes;

namespace Identity.UnitTests.Services;

public sealed class SessionServiceTests
{
    private static async Task<(IdentityHarness Harness, Account Account, string Refresh)> SignedInAsync()
    {
        var h = new IdentityHarness();
        var account = Account.RegisterWithPhone("skillmuni-in", "IN", "Yash", "P", "+919876543210", h.Now);
        h.Store.Accounts.Add(account);
        var signedIn = await h.Sessions.SignInAsync(account, CancellationToken.None);
        return (h, account, signedIn.Tokens.RefreshToken!);
    }

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var (h, _, refresh) = await SignedInAsync();
        h.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await h.Sessions.RefreshAsync(refresh, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(refresh, result.Value.RefreshToken);
        Assert.Equal(h.Now, Assert.Single(h.Store.Sessions).LastSeenAt);
    }

    [Fact]
    public async Task Thirty_minutes_idle_signs_out()
    {
        var (h, _, refresh) = await SignedInAsync();
        h.Clock.Advance(TimeSpan.FromMinutes(31));

        var result = await h.Sessions.RefreshAsync(refresh, CancellationToken.None);

        Assert.Equal("session-expired", result.Error?.Code);
        Assert.NotNull(Assert.Single(h.Store.Sessions).RevokedAt);
    }

    [Fact]
    public async Task Activity_keeps_the_session_alive_past_thirty_minutes()
    {
        var (h, _, refresh) = await SignedInAsync();
        for (var i = 0; i < 4; i++)
        {
            h.Clock.Advance(TimeSpan.FromMinutes(20));
            var result = await h.Sessions.RefreshAsync(refresh, CancellationToken.None);
            Assert.True(result.IsSuccess);
            refresh = result.Value.RefreshToken!;
        }
    }

    [Fact]
    public async Task Old_token_works_briefly_for_a_second_tab_then_revokes_the_session()
    {
        var (h, _, refresh) = await SignedInAsync();
        await h.Sessions.RefreshAsync(refresh, CancellationToken.None);

        var parallelTab = await h.Sessions.RefreshAsync(refresh, CancellationToken.None);
        Assert.True(parallelTab.IsSuccess);
        Assert.Null(parallelTab.Value.RefreshToken);

        h.Clock.Advance(TimeSpan.FromMinutes(2));
        var replay = await h.Sessions.RefreshAsync(refresh, CancellationToken.None);
        Assert.Equal("session-expired", replay.Error?.Code);
        Assert.NotNull(Assert.Single(h.Store.Sessions).RevokedAt);
    }

    [Fact]
    public async Task Logout_by_refresh_token_when_access_has_lapsed()
    {
        var (h, _, refresh) = await SignedInAsync();
        await h.Sessions.LogoutAsync(null, refresh, CancellationToken.None);
        Assert.NotNull(Assert.Single(h.Store.Sessions).RevokedAt);
    }

    [Fact]
    public async Task Current_session_returns_the_learner_or_no_session()
    {
        var (h, account, _) = await SignedInAsync();
        var session = Assert.Single(h.Store.Sessions);

        Assert.Equal("Yash", (await h.Sessions.CurrentAsync(account.Id, session.Id, CancellationToken.None)).Value.FirstName);
        Assert.Equal("no-session", (await h.Sessions.CurrentAsync(account.Id, Guid.NewGuid(), CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Erasure_removes_personal_details_and_every_session()
    {
        var (h, account, _) = await SignedInAsync();
        var result = await h.Privacy.EraseAsync(account.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(account.Phone);
        Assert.Equal(string.Empty, account.FirstName);
        Assert.All(h.Store.Sessions, s => Assert.NotNull(s.RevokedAt));
    }
}
