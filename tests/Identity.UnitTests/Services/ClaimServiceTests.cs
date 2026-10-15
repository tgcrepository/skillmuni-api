using Identity.Application.Contracts;
using Identity.Application.Events;
using Identity.UnitTests.Fakes;

namespace Identity.UnitTests.Services;

public sealed class ClaimServiceTests
{
    private static CreateClaimRequest Invite() => new(
        "Pune Skills Academy",
        "Aarav Mehta",
        "Customer Care Executive (Domestic Voice) · NSQF L4",
        "Pune batch 15 · completed 30 Sep 2026",
        "Contact, education, 8 skills, 2 languages, 1 job",
        "+91 98765 44521",
        "NSQF L4 certificate",
        new DateOnly(2026, 10, 30),
        [
            new ClaimSectionInput("who-dis", 1, "Who Dis?", "Aarav Mehta", [new ClaimRowDto("Name", "Aarav Mehta", "institute", null)]),
            new ClaimSectionInput("work-lore", 5, "Work Lore", "Customer support intern", [new ClaimRowDto("Intern", "Concentrix · Jun 2025 – ?", "institute", "End date missing.")]),
        ],
        new ClaimResultDto(80, 2, 9, 150, 6, [new ClaimLeftQuestDto(6, "pick-your-arc", "Pick Your Arc", "Jobs you want", 50)]));

    private static async Task<(IdentityHarness Harness, string Token)> CreatedAsync()
    {
        var h = new IdentityHarness();
        var created = await h.Claims.CreateAsync(Invite(), CancellationToken.None);
        return (h, created.Value.Token);
    }

    [Fact]
    public async Task Opening_the_link_sends_one_code()
    {
        var (h, token) = await CreatedAsync();

        var first = await h.Claims.GetInviteAsync(token, null, CancellationToken.None);
        var reload = await h.Claims.GetInviteAsync(token, null, CancellationToken.None);

        Assert.Equal("+91 98•••• 4521", first.Value.SentTo);
        Assert.Equal("2026-10-30", first.Value.DeleteBy);
        Assert.Null(first.Value.Resume);
        Assert.True(reload.IsSuccess);
        Assert.Single(h.Notifications.Sent);
    }

    [Fact]
    public async Task Unknown_link_is_404()
    {
        var h = new IdentityHarness();
        Assert.Equal("claim-not-found", (await h.Claims.GetInviteAsync("nope", null, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Full_claim_from_code_to_confirmed()
    {
        var (h, token) = await CreatedAsync();
        await h.Claims.GetInviteAsync(token, null, CancellationToken.None);

        var verified = await h.Claims.VerifyAsync(token, new ClaimCodeRequest("123456"), CancellationToken.None);
        Assert.Equal("Aarav", verified.Value.Body.FirstName);
        var learner = Guid.Parse(verified.Value.Body.Id);

        var sections = await h.Claims.ConsentAsync(token, learner, new ClaimConsentRequest(true, true, false), CancellationToken.None);
        Assert.Equal(new[] { "review", "fix" }, sections.Value.Select(s => s.Status));
        Assert.Equal(3, h.Store.Consents.Count);

        Assert.Equal("sections-pending", (await h.Claims.FinishAsync(token, learner, CancellationToken.None)).Error?.Code);
        Assert.True((await h.Claims.ConfirmSectionAsync(token, learner, "who-dis", CancellationToken.None)).IsSuccess);
        Assert.True((await h.Claims.ConfirmSectionAsync(token, learner, "work-lore", CancellationToken.None)).IsSuccess);
        Assert.Equal("section-not-found", (await h.Claims.ConfirmSectionAsync(token, learner, "nope", CancellationToken.None)).Error?.Code);

        var result = await h.Claims.FinishAsync(token, learner, CancellationToken.None);
        Assert.Equal(80, result.Value.ProfilePercent);

        var reopened = await h.Claims.GetInviteAsync(token, learner, CancellationToken.None);
        Assert.Equal("confirmed", reopened.Value.Resume?.Stage);
        Assert.Equal("claimed", (await h.Claims.GetInviteAsync(token, null, CancellationToken.None)).Error?.Code);

        Assert.Contains(h.Store.Events, e => e is LearnerRegistered);
        Assert.Contains(h.Store.Events, e => e is ClaimConsented);
        Assert.Contains(h.Store.Events, e => e is ClaimConfirmed);
    }

    [Fact]
    public async Task Someone_else_cannot_act_on_the_claim()
    {
        var (h, token) = await CreatedAsync();
        await h.Claims.GetInviteAsync(token, null, CancellationToken.None);
        await h.Claims.VerifyAsync(token, new ClaimCodeRequest("123456"), CancellationToken.None);

        var result = await h.Claims.ConsentAsync(token, Guid.NewGuid(), new ClaimConsentRequest(true, true, true), CancellationToken.None);
        Assert.Equal("not-claimant", result.Error?.Code);
    }

    [Fact]
    public async Task Decline_deletes_what_the_institute_added()
    {
        var (h, token) = await CreatedAsync();

        Assert.True((await h.Claims.DeclineAsync(token, new ClaimDeclineRequest("not-me"), CancellationToken.None)).IsSuccess);

        var invite = Assert.Single(h.Store.Claims);
        Assert.Equal(string.Empty, invite.LearnerName);
        Assert.Equal("[]", invite.SectionsJson);
        Assert.Equal("deleted", (await h.Claims.GetInviteAsync(token, null, CancellationToken.None)).Error?.Code);
        Assert.IsType<ClaimDeclined>(Assert.Single(h.Store.Events));
    }

    [Fact]
    public async Task Link_expires_on_the_delete_date()
    {
        var (h, token) = await CreatedAsync();
        h.Clock.Advance(TimeSpan.FromDays(18));
        Assert.Equal("expired", (await h.Claims.GetInviteAsync(token, null, CancellationToken.None)).Error?.Code);
    }
}
