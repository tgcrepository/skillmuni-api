using System.Net;
using Identity.Application.Abstractions;
using Identity.Domain.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.IntegrationTests;

[Collection(IdentityApiCollection.Name)]
public sealed class ClaimAndAccountEndpointsTests(IdentityApiFactory api)
{
    private async Task<HttpClient> InstituteAdminAsync()
    {
        var email = ApiAssertions.NewEmail();
        await api.SeedAsync((db, services) =>
        {
            var passwords = services.GetRequiredService<IPasswordHasher>();
            var admin = Account.RegisterWithEmail("skillmuni-in", "IN", "Admin", "PSA", email, passwords.Hash("admin-password"), DateTime.UtcNow);
            admin.GrantRole("institute-admin", DateTime.UtcNow);
            db.Accounts.Add(admin);
            return Task.CompletedTask;
        });

        var client = api.CreateLearnerClient();
        (await client.PostJsonAsync("/api/v1/auth/login", new { email, password = "admin-password" })).EnsureSuccessStatusCode();
        return client;
    }

    private async Task<string> CreateClaimAsync(string phone)
    {
        var admin = await InstituteAdminAsync();
        var created = await (await admin.PostJsonAsync("/api/v1/admin/claims", new
        {
            institute = "Pune Skills Academy",
            name = "Aarav Mehta",
            programme = "Customer Care Executive (Domestic Voice) · NSQF L4",
            batch = "Pune batch 15 · completed 30 Sep 2026",
            fromCv = "Contact, education, 8 skills, 2 languages, 1 job",
            phone,
            certificate = "NSQF L4 certificate",
            deleteBy = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            sections = new object[]
            {
                new { slug = "who-dis", quest = 1, title = "Who Dis?", summary = "Aarav Mehta", rows = new object[] { new { label = "Name", value = "Aarav Mehta", source = "institute" } } },
                new { slug = "work-lore", quest = 5, title = "Work Lore", summary = "Intern", rows = new object[] { new { label = "Intern", value = "Concentrix", source = "institute", issue = "End date missing." } } },
            },
            result = new { profilePercent = 80, verifiedItems = 2, jobs = 9, xpLeft = 150, minutesLeft = 6, left = Array.Empty<object>() },
        })).JsonAsync(HttpStatusCode.Created);
        return created.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Learner_claims_an_institute_profile()
    {
        var phone = ApiAssertions.NewPhone();
        var token = await CreateClaimAsync(phone);
        var learner = api.CreateLearnerClient();

        var invite = await (await learner.GetAsync($"/api/v1/claims/{token}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal($"+91 {phone[..2]}•••• {phone[^4..]}", invite.GetProperty("sentTo").GetString());
        Assert.False(invite.TryGetProperty("resume", out _));

        var wrong = await learner.PostJsonAsync($"/api/v1/claims/{token}/verify", new { code = "000001" });
        Assert.Equal("invalid-code", await wrong.ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        var user = await (await learner.PostJsonAsync($"/api/v1/claims/{token}/verify", new { code = IdentityApiFactory.FixedCode })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Aarav", user.GetProperty("firstName").GetString());

        var sections = await (await learner.PostJsonAsync($"/api/v1/claims/{token}/consent", new { shareProgress = true, verifyCertificate = true, applyForMe = false }))
            .JsonAsync(HttpStatusCode.OK);
        Assert.Equal("fix", sections[1].GetProperty("status").GetString());
        Assert.Equal("End date missing.", sections[1].GetProperty("rows")[0].GetProperty("issue").GetString());
        Assert.False(sections[0].GetProperty("rows")[0].TryGetProperty("issue", out _));

        var early = await learner.PostJsonAsync($"/api/v1/claims/{token}/finish", new { });
        Assert.Equal("sections-pending", await early.ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        foreach (var slug in new[] { "who-dis", "work-lore" })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await learner.PostJsonAsync($"/api/v1/claims/{token}/sections/{slug}/confirm", new { })).StatusCode);
        }

        var result = await (await learner.PostJsonAsync($"/api/v1/claims/{token}/finish", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(80, result.GetProperty("profilePercent").GetInt32());

        var reopened = await (await learner.GetAsync($"/api/v1/claims/{token}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("confirmed", reopened.GetProperty("resume").GetProperty("stage").GetString());

        var stranger = await api.CreateLearnerClient().GetAsync($"/api/v1/claims/{token}");
        Assert.Equal("claimed", await stranger.ProblemCodeAsync(HttpStatusCode.Gone));
    }

    [Fact]
    public async Task Declined_link_is_410_deleted_and_unknown_link_is_404()
    {
        var token = await CreateClaimAsync(ApiAssertions.NewPhone());
        var learner = api.CreateLearnerClient();

        Assert.Equal(HttpStatusCode.NoContent, (await learner.PostJsonAsync($"/api/v1/claims/{token}/decline", new { reason = "not-me" })).StatusCode);
        Assert.Equal("deleted", await (await learner.GetAsync($"/api/v1/claims/{token}")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.Equal("claim-not-found", await (await learner.GetAsync("/api/v1/claims/no-such-link")).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Learners_cannot_create_claims()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();
        await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "L", lastName = "L", email, password = "letmein123", consent = true });
        await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode });

        var response = await client.PostJsonAsync("/api/v1/admin/claims", new { });
        Assert.Equal("forbidden", await response.ProblemCodeAsync(HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task Consents_sessions_export_and_erasure()
    {
        var client = api.CreateLearnerClient();
        var email = ApiAssertions.NewEmail();
        await client.PostJsonAsync("/api/v1/auth/register", new { firstName = "Isha", lastName = "R", email, password = "letmein123", consent = true });
        await client.PostJsonAsync("/api/v1/auth/email/verify", new { email, code = IdentityApiFactory.FixedCode });

        var consent = await client.PostJsonAsync("/api/v1/me/consents", new { scope = "marketing.whatsapp", policyVersion = "2026-10", granted = true });
        Assert.Equal(HttpStatusCode.Created, consent.StatusCode);
        Assert.NotNull(consent.Headers.Location);

        var list = await (await client.GetAsync("/api/v1/me/consents?page=1&pageSize=10")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal("DPDP", list.GetProperty("items")[0].GetProperty("law").GetString());

        var sessions = await (await client.GetAsync("/api/v1/me/sessions")).JsonAsync(HttpStatusCode.OK);
        Assert.True(sessions.GetProperty("items")[0].GetProperty("current").GetBoolean());

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostJsonAsync("/api/v1/me/data-export", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/me")).StatusCode);

        var login = await api.CreateLearnerClient().PostJsonAsync("/api/v1/auth/email/login-code", new { email });
        Assert.Equal("account-not-found", await login.ProblemCodeAsync(HttpStatusCode.NotFound));
    }
}
