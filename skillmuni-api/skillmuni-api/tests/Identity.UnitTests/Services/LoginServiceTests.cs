using Identity.Application.Contracts;
using Identity.Domain.Accounts;
using Identity.UnitTests.Fakes;
using SkillMuni.Common.Results;

namespace Identity.UnitTests.Services;

public sealed class LoginServiceTests
{
    private const string Phone = "+919876543210";

    private static Account SeedPhoneAccount(IdentityHarness h)
    {
        var account = Account.RegisterWithPhone("skillmuni-in", "IN", "Yash", "P", Phone, h.Now);
        h.Store.Accounts.Add(account);
        return account;
    }

    [Fact]
    public async Task Unknown_mobile_is_account_not_found()
    {
        var h = new IdentityHarness();
        var result = await h.Login.SendPhoneCodeAsync(new SendPhoneCodeRequest("98765 43210"), CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("account-not-found", result.Error?.Code);
        Assert.Empty(h.Notifications.Sent);
    }

    [Fact]
    public async Task Locked_account_gets_423()
    {
        var h = new IdentityHarness();
        SeedPhoneAccount(h).Lock(h.Now);

        var result = await h.Login.SendPhoneCodeAsync(new SendPhoneCodeRequest(Phone), CancellationToken.None);
        Assert.Equal(ErrorKind.Locked, result.Error?.Kind);
    }

    [Fact]
    public async Task Code_goes_to_a_registered_mobile()
    {
        var h = new IdentityHarness();
        SeedPhoneAccount(h);

        var result = await h.Login.SendPhoneCodeAsync(new SendPhoneCodeRequest("98765 43210"), CancellationToken.None);

        Assert.Equal(new OtpChallengeDto("+91 98765 43210", 30, 5), result.Value);
        var sent = Assert.Single(h.Notifications.Sent);
        Assert.Equal(Phone, sent.To);
    }

    [Fact]
    public async Task Four_wrong_codes_count_down_then_429()
    {
        var h = new IdentityHarness();
        SeedPhoneAccount(h);
        await h.Login.SendPhoneCodeAsync(new SendPhoneCodeRequest(Phone), CancellationToken.None);

        for (var left = 4; left >= 1; left--)
        {
            var wrong = await h.Login.VerifyPhoneCodeAsync(new VerifyPhoneCodeRequest(Phone, "000001"), CancellationToken.None);
            Assert.Equal("invalid-code", wrong.Error?.Code);
            Assert.Equal(left.ToString(System.Globalization.CultureInfo.InvariantCulture), wrong.Error?.Details["triesLeft"][0]);
        }

        var fifth = await h.Login.VerifyPhoneCodeAsync(new VerifyPhoneCodeRequest(Phone, "000001"), CancellationToken.None);
        Assert.Equal("too-many-tries", fifth.Error?.Code);
        Assert.Equal(ErrorKind.RateLimited, fifth.Error?.Kind);
    }

    [Fact]
    public async Task Right_code_starts_a_session()
    {
        var h = new IdentityHarness();
        var account = SeedPhoneAccount(h);
        await h.Login.SendPhoneCodeAsync(new SendPhoneCodeRequest(Phone), CancellationToken.None);

        var result = await h.Login.VerifyPhoneCodeAsync(new VerifyPhoneCodeRequest(Phone, "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(account.Id.ToString(), result.Value.Body.Id);
        Assert.Equal("+91 98765 43210", result.Value.Body.Phone);
        Assert.NotNull(result.Value.Tokens.RefreshToken);
        Assert.Single(h.Store.Sessions);
    }

    [Fact]
    public async Task Wrong_password_is_401_and_locks_after_ten()
    {
        var h = new IdentityHarness();
        h.Store.Accounts.Add(Account.RegisterWithEmail("skillmuni-in", "IN", "Yash", "P", "yash@x.com", h.Passwords.Hash("letmein123"), h.Now));

        for (var i = 0; i < 10; i++)
        {
            var wrong = await h.Login.PasswordLoginAsync(new PasswordLoginRequest("yash@x.com", "nope"), CancellationToken.None);
            Assert.Equal("wrong-credentials", wrong.Error?.Code);
        }

        var locked = await h.Login.PasswordLoginAsync(new PasswordLoginRequest("yash@x.com", "letmein123"), CancellationToken.None);
        Assert.Equal("account-locked", locked.Error?.Code);

        h.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.True((await h.Login.PasswordLoginAsync(new PasswordLoginRequest("yash@x.com", "letmein123"), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Password_reset_answers_the_same_for_unknown_emails()
    {
        var h = new IdentityHarness();
        var answer = await h.Login.RequestPasswordResetAsync(new PasswordResetRequest("nobody@x.com"), CancellationToken.None);

        Assert.Equal(new ResetRequestedDto(45, 30), answer);
        Assert.Empty(h.Notifications.Sent);
    }

    [Fact]
    public async Task Password_reset_link_changes_the_password_and_signs_out_everywhere()
    {
        var h = new IdentityHarness();
        var account = Account.RegisterWithEmail("skillmuni-in", "IN", "Yash", "P", "yash@x.com", h.Passwords.Hash("old-password"), h.Now);
        h.Store.Accounts.Add(account);
        await h.Sessions.SignInAsync(account, CancellationToken.None);

        await h.Login.RequestPasswordResetAsync(new PasswordResetRequest("yash@x.com"), CancellationToken.None);
        var token = Assert.Single(h.Notifications.Sent).Secret;
        var result = await h.Login.ConfirmPasswordResetAsync(new PasswordResetConfirmRequest(token, "new-password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(h.Passwords.Verify(account.PasswordHash!, "new-password"));
        Assert.All(h.Store.Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.Equal("code-expired", (await h.Login.ConfirmPasswordResetAsync(new PasswordResetConfirmRequest(token, "again-again"), CancellationToken.None)).Error?.Code);
    }
}
