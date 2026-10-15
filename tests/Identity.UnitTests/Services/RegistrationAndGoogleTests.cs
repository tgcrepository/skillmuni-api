using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Events;
using Identity.Domain.Accounts;
using Identity.UnitTests.Fakes;

namespace Identity.UnitTests.Services;

public sealed class RegistrationAndGoogleTests
{
    private static RegisterRequest Join(string email = "new.learner@gmail.com", bool consent = true) =>
        new("Asha", "Rao", email, "letmein123", consent);

    [Fact]
    public async Task Consent_is_required()
    {
        var h = new IdentityHarness();
        var result = await h.Registration.RegisterAsync(Join(consent: false), CancellationToken.None);
        Assert.Equal("consent-required", result.Error?.Code);
    }

    [Fact]
    public async Task Taken_email_is_409_with_the_field_message()
    {
        var h = new IdentityHarness();
        h.Store.Accounts.Add(Account.RegisterWithEmail("skillmuni-in", "IN", "A", "B", "taken@gmail.com", "p:x", h.Now));

        var result = await h.Registration.RegisterAsync(Join("Taken@Gmail.com"), CancellationToken.None);

        Assert.Equal("email-taken", result.Error?.Code);
        Assert.Equal("Already in the squad.", result.Error?.Details["email"][0]);
    }

    [Fact]
    public async Task Verifying_creates_account_consent_event_and_session_in_one_save()
    {
        var h = new IdentityHarness();
        var challenge = await h.Registration.RegisterAsync(Join(), CancellationToken.None);
        Assert.Equal(new EmailChallengeDto("new.learner@gmail.com", 30, 10, 5), challenge.Value);

        var saves = h.Store.Saves;
        var result = await h.Registration.VerifyAsync(new VerifyEmailCodeRequest("new.learner@gmail.com", "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(saves + 1, h.Store.Saves);
        var account = Assert.Single(h.Store.Accounts);
        Assert.Equal("Asha", result.Value.Body.FirstName);
        Assert.Equal("p:letmein123", account.PasswordHash);
        var consent = Assert.Single(h.Store.Consents);
        Assert.Equal(("registration", "DPDP", true), (consent.Scope, consent.Law, consent.Granted));
        var registered = Assert.IsType<LearnerRegistered>(Assert.Single(h.Store.Events));
        Assert.Equal(account.Id, registered.AccountId);
        Assert.Empty(h.Store.Pending);
        Assert.Single(h.Store.Sessions);
    }

    [Fact]
    public async Task Resend_without_sign_up_is_404()
    {
        var h = new IdentityHarness();
        var result = await h.Registration.ResendAsync(new SendEmailCodeRequest("nobody@gmail.com"), CancellationToken.None);
        Assert.Equal("registration-not-found", result.Error?.Code);
    }

    [Fact]
    public async Task Google_creates_then_recognises_the_learner()
    {
        var h = new IdentityHarness();
        h.GoogleValidator.Tokens["cred"] = new GoogleIdentity("google-sub-1", "Priya@Gmail.com", true, "Priya", "D");

        var first = await h.Google.SignInAsync(new GoogleSignInRequest("cred"), CancellationToken.None);
        var second = await h.Google.SignInAsync(new GoogleSignInRequest("cred"), CancellationToken.None);

        Assert.True(first.Value.Body.IsNew);
        Assert.False(second.Value.Body.IsNew);
        Assert.Equal("priya@gmail.com", first.Value.Body.User.Email);
        Assert.Single(h.Store.Accounts);
    }

    [Fact]
    public async Task Google_links_an_existing_email_account()
    {
        var h = new IdentityHarness();
        h.Store.Accounts.Add(Account.RegisterWithEmail("skillmuni-in", "IN", "Priya", "D", "priya@gmail.com", "p:x", h.Now));
        h.GoogleValidator.Tokens["cred"] = new GoogleIdentity("google-sub-1", "priya@gmail.com", true, "Priya", "D");

        var result = await h.Google.SignInAsync(new GoogleSignInRequest("cred"), CancellationToken.None);

        Assert.False(result.Value.Body.IsNew);
        Assert.Equal("google-sub-1", Assert.Single(h.Store.Accounts).GoogleSubject);
    }

    [Fact]
    public async Task Google_rejects_bad_tokens_and_missing_client_id()
    {
        var h = new IdentityHarness();
        Assert.Equal("google-invalid", (await h.Google.SignInAsync(new GoogleSignInRequest("forged"), CancellationToken.None)).Error?.Code);

        h.TenantSettings.GoogleClientId = null;
        Assert.Equal("google-unavailable", (await h.Google.SignInAsync(new GoogleSignInRequest("forged"), CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Verified_mobile_can_log_in_afterwards()
    {
        var h = new IdentityHarness();
        var account = Account.RegisterWithEmail("skillmuni-in", "IN", "Asha", "R", "asha@gmail.com", "p:x", h.Now);
        h.Store.Accounts.Add(account);

        var sent = await h.Contacts.SendPhoneCodeAsync(account.Id, new SendPhoneCodeRequest("91234 56789"), CancellationToken.None);
        Assert.Equal("+91 91234 56789", sent.Value.SentTo);
        var verified = await h.Contacts.VerifyPhoneAsync(account.Id, new VerifyPhoneCodeRequest("91234 56789", "123456"), CancellationToken.None);

        Assert.True(verified.IsSuccess);
        Assert.Equal("+919123456789", account.Phone);
        Assert.IsType<ContactVerified>(Assert.Single(h.Store.Events));
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True((await h.Login.SendPhoneCodeAsync(new SendPhoneCodeRequest("9123456789"), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task A_number_on_another_account_is_409()
    {
        var h = new IdentityHarness();
        h.Store.Accounts.Add(Account.RegisterWithPhone("skillmuni-in", "IN", "Other", "", "+919123456789", h.Now));
        var me = Account.RegisterWithEmail("skillmuni-in", "IN", "Asha", "R", "asha@gmail.com", "p:x", h.Now);
        h.Store.Accounts.Add(me);

        var result = await h.Contacts.SendContactCodeAsync(me.Id, new ContactCodeRequest("mobile", "9123456789"), CancellationToken.None);
        Assert.Equal("phone-taken", result.Error?.Code);
    }

    [Fact]
    public async Task Contact_code_screen_shows_a_masked_value()
    {
        var h = new IdentityHarness();
        var me = Account.RegisterWithEmail("skillmuni-in", "IN", "Asha", "R", "asha@gmail.com", "p:x", h.Now);
        h.Store.Accounts.Add(me);

        var result = await h.Contacts.SendContactCodeAsync(me.Id, new ContactCodeRequest("email", "asha.new@gmail.com"), CancellationToken.None);

        Assert.Equal("as•••@gmail.com", result.Value.SentTo);
        Assert.Equal(NotificationChannel.Email, Assert.Single(h.Notifications.Sent).Channel);
    }
}
