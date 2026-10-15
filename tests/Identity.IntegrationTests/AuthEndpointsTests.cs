using System.Net;
using System.Text.Json;
using Identity.Domain.Accounts;
using Identity.Domain.Codes;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Messaging.Outbox;

namespace Identity.IntegrationTests;

[Collection(IdentityApiCollection.Name)]
public sealed class AuthEndpointsTests(IdentityApiFactory api)
{
    private const string Tenant = "skillmuni-in";

    private Task SeedPhoneAccountAsync(string phone, bool locked = false) => api.SeedAsync((db, _) =>
    {
        var account = Account.RegisterWithPhone(Tenant, "IN", "Yash", "P", $"+91{phone}", DateTime.UtcNow);
        if (locked)
        {
            account.Lock(DateTime.UtcNow);
        }

        db.Accounts.Add(account);
        return Task.CompletedTask;
    });

    [Fact]
    public async Task Sign_up_verify_session_logout()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();

        var challenge = await (await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Asha", lastName = "Rao", email, password = "letmein123", consent = true }))
            .JsonAsync(HttpStatusCode.OK);
        Assert.Equal(email, challenge.GetProperty("email").GetString());
        Assert.Equal(20, challenge.GetProperty("triesLeft").GetInt32());

        var user = await (await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode }))
            .JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Asha", user.GetProperty("firstName").GetString());
        Assert.False(user.TryGetProperty("phone", out _));

        var session = await (await client.GetAsync("/api/v1/session")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(user.GetProperty("id").GetString(), session.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("photoUrl").ValueKind);

        var events = await api.QueryAsync(db => db.Set<OutboxMessage>().Where(m => m.Payload.Contains(email)).Select(m => m.EventType).ToListAsync());
        Assert.Contains("identity.learner.registered", events);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal("no-session", await (await client.GetAsync("/api/v1/session")).ProblemCodeAsync(HttpStatusCode.Unauthorized));

        var again = await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Asha", lastName = "Rao", email, password = "letmein123", consent = true });
        Assert.Equal("email-taken", await again.ProblemCodeAsync(HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Unregistered_mobile_is_404_account_not_found()
    {
        var client = api.CreateLearnerClient();
        var response = await client.PostJsonAsync("/api/v1/auth/otp", new { phone = ApiAssertions.NewPhone() });
        Assert.Equal("account-not-found", await response.ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Locked_account_is_423()
    {
        var phone = ApiAssertions.NewPhone();
        await SeedPhoneAccountAsync(phone, locked: true);
        var response = await api.CreateLearnerClient().PostJsonAsync("/api/v1/auth/otp", new { phone });
        Assert.Equal("account-locked", await response.ProblemCodeAsync(HttpStatusCode.Locked));
    }

    [Fact]
    public async Task Learner_photo_event_sets_the_session_photo_url()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();
        await (await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Asha", lastName = "Rao", email, password = "letmein123", consent = true })).JsonAsync(HttpStatusCode.OK);
        var user = await (await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode })).JsonAsync(HttpStatusCode.OK);
        var id = Guid.Parse(user.GetProperty("id").GetString()!);

        var photo = new { learnerId = id, photoUrl = "/api/v1/profile/photo?v=1", tenantId = "skillmuni-in" };
        var eventId = Guid.NewGuid();
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("learner.photo.changed", photo, eventId));
        Assert.Equal(DispatchOutcome.Duplicate, await api.DeliverAsync("learner.photo.changed", photo, eventId));

        var session = await (await client.GetAsync("/api/v1/session")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("/api/v1/profile/photo?v=1", session.GetProperty("photoUrl").GetString());
    }

    [Fact]
    public async Task Internal_email_owner_lookup_takes_the_email_in_the_body()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();
        await (await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Asha", lastName = "Rao", email, password = "letmein123", consent = true })).JsonAsync(HttpStatusCode.OK);
        var user = await (await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode })).JsonAsync(HttpStatusCode.OK);

        var service = api.CreateClient();
        var owner = await (await service.PostJsonAsync("/internal/accounts/email-owner", new { email })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(user.GetProperty("id").GetString(), owner.GetProperty("accountId").GetString());
        Assert.Equal("account-not-found", await (await service.PostJsonAsync("/internal/accounts/email-owner", new { email = ApiAssertions.NewEmail() })).ProblemCodeAsync(HttpStatusCode.NotFound));

        // No personal data in URLs: the old query-string form is gone.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await service.GetAsync($"/internal/accounts/email-owner?email={Uri.EscapeDataString(email)}")).StatusCode);
    }

    [Fact]
    public async Task Wrong_passwords_never_lock_the_account()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();
        await (await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Asha", lastName = "Rao", email, password = "letmein123", consent = true })).JsonAsync(HttpStatusCode.OK);
        await (await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode })).JsonAsync(HttpStatusCode.OK);

        for (var i = 0; i < 12; i++)
        {
            Assert.Equal("wrong-credentials", await (await client.PostJsonAsync("/api/v1/auth/login", new { email, password = "nope-nope" })).ProblemCodeAsync(HttpStatusCode.Unauthorized));
        }

        await (await client.PostJsonAsync("/api/v1/auth/login", new { email, password = "letmein123" })).JsonAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hidden_limit_of_20_wrong_codes_then_429()
    {
        var phone = ApiAssertions.NewPhone();
        await SeedPhoneAccountAsync(phone);
        var client = api.CreateLearnerClient();

        var challenge = await (await client.PostJsonAsync("/api/v1/auth/otp", new { phone })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal($"+91 {phone[..5]} {phone[5..]}", challenge.GetProperty("sentTo").GetString());
        Assert.Equal(30, challenge.GetProperty("resendIn").GetInt32());

        Assert.Equal(20, challenge.GetProperty("triesLeft").GetInt32());
        for (var left = 19; left >= 1; left--)
        {
            var wrong = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code = "000001" });
            var json = await wrong.JsonAsync(HttpStatusCode.UnprocessableEntity);
            Assert.Equal("invalid-code", json.GetProperty("errors").GetProperty("code")[0].GetString());
            Assert.Equal(left.ToString(System.Globalization.CultureInfo.InvariantCulture), json.GetProperty("errors").GetProperty("triesLeft")[0].GetString());
        }

        var last = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code = "000001" });
        Assert.Equal("too-many-tries", await last.ProblemCodeAsync(HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Expired_code_is_410()
    {
        var phone = ApiAssertions.NewPhone();
        await SeedPhoneAccountAsync(phone);
        var client = api.CreateLearnerClient();
        await client.PostJsonAsync("/api/v1/auth/otp", new { phone });
        await api.QueryAsync(db => db.VerificationCodes
            .Where(c => c.Purpose == CodePurpose.LoginPhone && c.Target == $"+91{phone}")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        var response = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code = IdentityApiFactory.FixedCode });
        Assert.Equal("code-expired", await response.ProblemCodeAsync(HttpStatusCode.Gone));
    }

    [Fact]
    public async Task Email_login_code_and_password_login()
    {
        var email = ApiAssertions.NewEmail();
        var client = api.CreateLearnerClient();
        await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Ravi", lastName = "K", email, password = "letmein123", consent = true });
        await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode });

        var other = api.CreateLearnerClient();
        var wrong = await other.PostJsonAsync("/api/v1/auth/login", new { email, password = "nope-nope" });
        Assert.Equal("wrong-credentials", await wrong.ProblemCodeAsync(HttpStatusCode.Unauthorized));
        (await other.PostJsonAsync("/api/v1/auth/login", new { email, password = "letmein123" })).EnsureSuccessStatusCode();

        var third = api.CreateLearnerClient();
        var code = await (await third.PostJsonAsync("/api/v1/auth/email/login-code", new { email })).JsonAsync(HttpStatusCode.OK);
        Assert.StartsWith(email[..2] + "•••@", code.GetProperty("sentTo").GetString(), StringComparison.Ordinal);
        (await third.PostJsonAsync("/api/v1/auth/email/login-verify", new { email, code = IdentityApiFactory.FixedCode })).EnsureSuccessStatusCode();
        (await third.GetAsync("/api/v1/session")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Idle_for_thirty_minutes_ends_the_session()
    {
        var phone = ApiAssertions.NewPhone();
        await SeedPhoneAccountAsync(phone);
        var client = api.CreateLearnerClient();
        await client.PostJsonAsync("/api/v1/auth/otp", new { phone });
        var user = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code = IdentityApiFactory.FixedCode })).JsonAsync(HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/auth/refresh", new { })).StatusCode);

        var accountId = Guid.Parse(user.GetProperty("id").GetString()!);
        await api.QueryAsync(db => db.Sessions
            .Where(s => s.AccountId == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, DateTime.UtcNow.AddMinutes(-31))));

        var refresh = await client.PostJsonAsync("/api/v1/auth/refresh", new { });
        Assert.Equal("session-expired", await refresh.ProblemCodeAsync(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Google_sign_in_says_whether_the_learner_is_new()
    {
        var credential = $"google:{Guid.NewGuid():N}:{ApiAssertions.NewEmail()}:Priya";
        var first = await (await api.CreateLearnerClient().PostJsonAsync("/api/v1/auth/google", new { credential })).JsonAsync(HttpStatusCode.OK);
        var second = await (await api.CreateLearnerClient().PostJsonAsync("/api/v1/auth/google", new { credential })).JsonAsync(HttpStatusCode.OK);

        Assert.True(first.GetProperty("isNew").GetBoolean());
        Assert.False(second.GetProperty("isNew").GetBoolean());
        Assert.Equal("Priya", second.GetProperty("user").GetProperty("firstName").GetString());

        var forged = await api.CreateLearnerClient().PostJsonAsync("/api/v1/auth/google", new { credential = "forged" });
        Assert.Equal("google-invalid", await forged.ProblemCodeAsync(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Verified_mobile_can_then_log_in()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();
        await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Meera", lastName = "J", email, password = "letmein123", consent = true });
        await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode });

        var phone = ApiAssertions.NewPhone();
        (await client.PostJsonAsync("/api/v1/me/phone/code", new { phone })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/me/phone/verify", new { phone, code = IdentityApiFactory.FixedCode })).StatusCode);

        var login = await api.CreateLearnerClient().PostJsonAsync("/api/v1/auth/otp", new { phone });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Rule_breaking_body_is_422_and_unreadable_body_is_400()
    {
        var client = api.CreateLearnerClient();
        var invalid = await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "A", lastName = "B", email = "nope", password = "short", consent = true });
        var json = await invalid.JsonAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("validation", json.GetProperty("errors").GetProperty("code")[0].GetString());
        Assert.True(json.GetProperty("errors").TryGetProperty("email", out _));
        Assert.True(json.GetProperty("errors").TryGetProperty("password", out _));

        using var broken = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        var unreadable = await client.PostAsync("/api/v1/auth/otp", broken);
        Assert.Equal("malformed-request", await unreadable.ProblemCodeAsync(HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Protected_endpoints_need_a_session()
    {
        var response = await api.CreateLearnerClient().PostJsonAsync("/api/v1/me/phone/code", new { phone = ApiAssertions.NewPhone() });
        Assert.Equal("no-session", await response.ProblemCodeAsync(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        var client = api.CreateLearnerClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
