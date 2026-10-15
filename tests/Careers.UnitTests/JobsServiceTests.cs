using Careers.Application;
using Careers.Application.Contracts;
using Careers.Application.Services;
using Careers.Domain;
using Careers.UnitTests.Fakes;

namespace Careers.UnitTests;

public sealed class JobsServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly CareersHarness _h = new();

    private static ListQuery Query(string filters = "{}", string q = "", string place = "", string sort = "", int limit = 20) =>
        ListEngine.ParseQuery(q, place, sort, limit, filters, 200);

    [Fact]
    public async Task Unknown_skills_mean_no_matches_yet()
    {
        var summary = await _h.Jobs.SummaryAsync(_h.Me, None);

        Assert.Equal(128, summary.Available);
        Assert.Equal(0, summary.Matching);
        Assert.Equal(0, summary.UnlockAt);
        Assert.Equal(string.Empty, summary.DefaultPlace);
        Assert.NotNull(_h.Store.Learners.Single().JobsSeenAt);

        var matching = await _h.Jobs.JobsAsync(_h.Me, "matching", Query(), None);
        Assert.Equal(0, matching.Value.Total);
        Assert.Null((await _h.Jobs.JobAsync(_h.Me, "job-1", None)).Value.Match);
    }

    [Fact]
    public async Task Matching_lists_jobs_at_60_percent_or_more_best_first()
    {
        _h.Learner.Profile = CareersHarness.PuneProfile();

        var summary = await _h.Jobs.SummaryAsync(_h.Me, None);
        var matching = (await _h.Jobs.JobsAsync(_h.Me, "matching", Query(limit: 200), None)).Value;

        Assert.Equal("Pune and Remote", summary.DefaultPlace);
        Assert.Equal(summary.Matching, matching.Total);
        Assert.True(matching.Total > 0);
        Assert.All(matching.Items, j => Assert.True(j.Match!.Percent >= 60));
        Assert.Equal(matching.Items.OrderByDescending(j => j.Match!.Percent).Select(j => j.Id), matching.Items.Select(j => j.Id));
        Assert.Equal("match", matching.Facets[0].Key);
    }

    [Fact]
    public async Task Search_place_and_facets_follow_the_mock()
    {
        var list = (await _h.Jobs.JobsAsync(_h.Me, "available", Query(place: "Pune and Remote"), None)).Value;
        Assert.Equal(66, list.Total);
        Assert.Equal(20, list.Items.Count);

        var bySector = (await _h.Jobs.JobsAsync(_h.Me, "available", Query("""{"sector":["Retail"]}"""), None)).Value;
        var sectorFacet = bySector.Facets.Single(f => f.Key == "sector");
        Assert.Equal(128, sectorFacet.Options.Sum(o => o.Count));
        Assert.Equal(bySector.Total, sectorFacet.Options.Single(o => o.Id == "Retail").Count);
        Assert.Equal(new[] { "Store associate", "Cashier", "Visual merchandiser" }, bySector.Facets.Single(f => f.Key == "role").Options.Select(o => o.Id));

        var nothing = (await _h.Jobs.JobsAsync(_h.Me, "available", Query("""{"sector":["Retail"],"workMode":["remote"],"jobType":["contract"]}"""), None)).Value;
        if (nothing.Total == 0)
        {
            Assert.NotEmpty(nothing.Suggestions);
        }

        Assert.Equal(CareersErrors.InvalidTab, (await _h.Jobs.JobsAsync(_h.Me, "galaxy", Query(), None)).Error);
    }

    [Fact]
    public async Task Bookmarks_show_in_the_bookmarked_tab_with_a_note()
    {
        Assert.True((await _h.Jobs.SaveAsync(_h.Me, "job-61", true, None)).Value.Saved);
        await _h.Jobs.SaveAsync(_h.Me, "job-2", true, None);
        await _h.Jobs.SaveAsync(_h.Me, "job-2", true, None);

        var saved = (await _h.Jobs.JobsAsync(_h.Me, "bookmarked", Query(), None)).Value;
        Assert.Equal(2, saved.Total);
        Assert.Equal("2 saved jobs · 2 closing soon", saved.Note);
        Assert.Equal("jobStatus", saved.Facets[0].Key);

        Assert.False((await _h.Jobs.SaveAsync(_h.Me, "job-2", false, None)).Value.Saved);
        Assert.Equal(CareersErrors.JobNotFound, (await _h.Jobs.SaveAsync(_h.Me, "job-999", true, None)).Error);
    }

    [Fact]
    public async Task Apply_sends_once_and_publishes_the_event()
    {
        var cv = await _h.UploadCvAsync();
        var request = new ApplyRequest(cv.Id.ToString(), new Dictionary<string, string> { ["q1"] = "Yes", ["q2"] = "Immediately", ["zz"] = "ignored" }, null);

        var sent = await _h.Jobs.ApplyAsync(_h.Me, "job-1", request, None);

        Assert.True(sent.IsSuccess);
        Assert.Equal("applied", sent.Value.Status);
        Assert.Equal("15 Oct", sent.Value.AppliedOn);
        Assert.Equal("15 Oct", sent.Value.Job.AppliedOn);
        Assert.Equal("Yash_CV.pdf", sent.Value.Cv);
        Assert.Equal(2, sent.Value.Answers.Count);
        Assert.Equal("Updated today", sent.Value.Updated);
        var e = Assert.IsType<ApplicationSent>(Assert.Single(_h.Store.Events));
        Assert.Equal("job-1", e.JobId);
        Assert.Equal(CareersErrors.AlreadyApplied, (await _h.Jobs.ApplyAsync(_h.Me, "job-1", request, None)).Error);
        Assert.Equal(CareersErrors.CvNotFound, (await _h.Jobs.ApplyAsync(_h.Me, "job-2", request with { CvId = Guid.NewGuid().ToString() }, None)).Error);
    }

    [Fact]
    public async Task Withdraw_then_reapply_after_the_cool_off()
    {
        var cv = await _h.UploadCvAsync();
        var request = new ApplyRequest(cv.Id.ToString(), null, null);
        var sent = (await _h.Jobs.ApplyAsync(_h.Me, "job-3", request, None)).Value;

        var withdrawn = await _h.Jobs.WithdrawAsync(_h.Me, sent.Id, None);
        Assert.Equal("withdrawn", withdrawn.Value.Status);
        Assert.Equal("You withdrew on 15 Oct", withdrawn.Value.Updated);
        Assert.Null(withdrawn.Value.Job.AppliedOn);
        Assert.IsType<ApplicationWithdrawn>(_h.Store.Events[^1]);
        await _h.Jobs.WithdrawAsync(_h.Me, sent.Id, None);
        Assert.Equal(2, _h.Store.Events.Count);

        Assert.Equal(CareersErrors.ReapplyCoolOff, (await _h.Jobs.ApplyAsync(_h.Me, "job-3", request, None)).Error);
        _h.Time.Advance(TimeSpan.FromDays(31));
        Assert.True((await _h.Jobs.ApplyAsync(_h.Me, "job-3", request, None)).IsSuccess);

        var applied = await _h.Jobs.ApplicationsAsync(_h.Me, Query(), None);
        Assert.Equal(2, applied.Total);
        Assert.Equal("2 applications", applied.Note);
    }

    [Fact]
    public async Task Offers_cannot_be_withdrawn()
    {
        var offer = JobApplication.FestOffer(CareersHarness.Tenant, _h.Me, "job-1", "Voice Process Exec", "tech-bpo-pune:tp", "CV", _h.Time.GetUtcNow().UtcDateTime, _h.Time.GetUtcNow().UtcDateTime);
        _h.Store.Applications.Add(offer);

        Assert.Equal(CareersErrors.CannotWithdraw, (await _h.Jobs.WithdrawAsync(_h.Me, offer.Id.ToString(), None)).Error);
        var dto = (await _h.Jobs.ApplicationAsync(_h.Me, offer.Id.ToString(), None)).Value;
        Assert.Equal("Voice Process Exec", dto.Job.Title);
        Assert.Equal("Offer from Hire Fest", dto.Updated);
        Assert.All(dto.Timeline, s => Assert.Equal("done", s.State));
    }

    [Fact]
    public async Task Invitations_respond_and_expire()
    {
        var invite = _h.Invite("job-1");
        var old = _h.Invite("job-6", respondInDays: -1);

        var list = await _h.Jobs.InvitationsAsync(_h.Me, Query(), None);
        Assert.Equal("1 new · 2 total", list.Note);
        Assert.Equal("expired", list.Items.Single(i => i.Id == old.Id.ToString()).Status);
        Assert.Equal(7, list.Items.Single(i => i.Id == invite.Id.ToString()).DaysLeft);

        var suggested = await _h.Jobs.RespondAsync(_h.Me, invite.Id.ToString(), "suggest", "Can we do Monday?", None);
        Assert.Equal("new", suggested.Value.Status);
        Assert.Equal("Can we do Monday?", invite.Note);
        Assert.Equal("declined", (await _h.Jobs.RespondAsync(_h.Me, invite.Id.ToString(), "decline", null, None)).Value.Status);
        Assert.Equal(CareersErrors.InvitationExpired, (await _h.Jobs.RespondAsync(_h.Me, old.Id.ToString(), "accept", null, None)).Error);
        Assert.Equal(CareersErrors.InvitationNotFound, (await _h.Jobs.InvitationAsync(_h.Me, "inv-1", None)).Error);
    }

    [Fact]
    public async Task Applying_from_an_invitation_accepts_it()
    {
        var cv = await _h.UploadCvAsync();
        var invite = _h.Invite("job-2");

        var sent = await _h.Jobs.ApplyAsync(_h.Me, "job-2", new ApplyRequest(cv.Id.ToString(), null, invite.Id.ToString()), None);

        Assert.True(sent.IsSuccess);
        Assert.Equal(InvitationStatus.Accepted, invite.Status);
    }

    [Fact]
    public async Task Cv_upload_checks_type_and_size()
    {
        using var text = new MemoryStream("hello"u8.ToArray());
        Assert.Equal(CareersErrors.UnsupportedType, (await _h.Jobs.UploadCvAsync(_h.Me, new UploadedFile("cv.pdf", text.Length, text), None)).Error);
        Assert.Equal(CareersErrors.FileMissing, (await _h.Jobs.UploadCvAsync(_h.Me, null, None)).Error);
        using var big = new MemoryStream(new byte[10]);
        Assert.Equal(CareersErrors.TooLarge, (await _h.Jobs.UploadCvAsync(_h.Me, new UploadedFile("cv.pdf", 6 * 1024 * 1024, big), None)).Error);

        var cv = await _h.UploadCvAsync();
        Assert.True(_h.Store.Blobs.ContainsKey(cv.BlobName));
        var info = (await _h.Jobs.ApplyInfoAsync(_h.Me, "job-1", "Yash", None)).Value;
        Assert.Equal("Yash", info.Name);
        Assert.Equal("Uploaded today · 1 KB", Assert.Single(info.Cvs).Meta);
        Assert.Equal(new[] { "q1", "q2" }, info.Questions.Select(q => q.Id));
    }

    [Fact]
    public async Task Alerts_default_off_and_save()
    {
        var alerts = await _h.Jobs.AlertsAsync(_h.Me, None);
        Assert.False(alerts.On);
        Assert.Empty(alerts.Searches);

        var saved = await _h.Jobs.SetAlertsAsync(_h.Me, new AlertSettingsDto(true, "weekly", ["Customer service · Pune", " Customer service · Pune "]), None);
        Assert.Equal(new[] { "Customer service · Pune" }, saved.Searches);
        Assert.Equal("weekly", (await _h.Jobs.AlertsAsync(_h.Me, None)).Frequency);
    }
}
