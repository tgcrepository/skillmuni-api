using Careers.Application;
using Careers.Application.Abstractions;
using Careers.Application.Contracts;
using Careers.UnitTests.Fakes;

namespace Careers.UnitTests;

public sealed class LaunchpadAndMatchesTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly CareersHarness _h = new();

    private AssessmentSubmitted Submitted(int attempt = 1) =>
        new(_h.Me, attempt, attempt == 1, "The People-First Service Builder", "You light up when you help people.", ["People", "Helping", "Structure", "Security"]) { TenantId = CareersHarness.Tenant };

    [Fact]
    public async Task A_new_learner_is_on_the_launchpad()
    {
        var readiness = await _h.Launchpad.ReadinessAsync(_h.Me, None);

        Assert.Equal(0, readiness.Level);
        Assert.Equal("On the launchpad", readiness.LevelName);
        Assert.Equal(0, readiness.Percent);
        Assert.Equal(new[] { "assessment", "profile", "apply", "fest" }, readiness.Missions.Select(m => m.Id));
        Assert.Equal(new[] { 100, 50, 100, 100 }, readiness.Missions.Select(m => m.Xp));

        var assessment = await _h.Launchpad.AssessmentAsync(_h.Me, None);
        Assert.Equal("not-started", assessment.Status);
        Assert.Equal(36, assessment.Total);
        Assert.All(await _h.Launchpad.SectorsAsync(_h.Me, None), s => Assert.Null(s.Fit));
    }

    [Fact]
    public async Task Missions_tick_from_events_and_activity()
    {
        await _h.Assessment.HandleAsync(Submitted(), None);
        var one = await _h.Launchpad.ReadinessAsync(_h.Me, None);
        Assert.Equal(1, one.Level);
        Assert.Equal("Engines warming up", one.LevelName);
        Assert.Equal(29, one.Percent);

        await _h.JobProfile.HandleAsync(new JobProfileCompleted(_h.Me) { TenantId = CareersHarness.Tenant }, None);
        var cv = await _h.UploadCvAsync();
        await _h.Jobs.ApplyAsync(_h.Me, "job-1", new ApplyRequest(cv.Id.ToString(), null, null), None);
        _h.Learner.Progress = FakeLearnerDirectory.Percent(100);
        await _h.Fests.BookAsync(_h.Me, "retail-mumbai", CareersHarness.Book(("rt", "2026-12-20", "11:00")), None);

        var all = await _h.Launchpad.ReadinessAsync(_h.Me, None);
        Assert.Equal(4, all.Level);
        Assert.Equal("Launched!", all.LevelName);
        Assert.Equal(100, all.Percent);

        var card = await _h.Launchpad.FestAsync(_h.Me, None);
        Assert.Equal("retail-mumbai", card.Fest!.Id);
        Assert.Equal("11:00 AM", card.Fest.BookedSlot);
        Assert.Equal(80, card.Fest.SlotsTotal);
        Assert.Equal(33, card.Fest.SlotsLeft);
    }

    [Fact]
    public async Task Withdrawing_un_ticks_the_apply_mission()
    {
        var cv = await _h.UploadCvAsync();
        var sent = (await _h.Jobs.ApplyAsync(_h.Me, "job-1", new ApplyRequest(cv.Id.ToString(), null, null), None)).Value;
        Assert.True((await _h.Launchpad.ReadinessAsync(_h.Me, None)).Missions.Single(m => m.Id == "apply").Done);

        await _h.Jobs.WithdrawAsync(_h.Me, sent.Id, None);
        Assert.False((await _h.Launchpad.ReadinessAsync(_h.Me, None)).Missions.Single(m => m.Id == "apply").Done);
    }

    [Fact]
    public async Task Assessment_card_and_sectors_after_the_result()
    {
        await _h.Assessment.HandleAsync(Submitted(2), None);
        await _h.Assessment.HandleAsync(Submitted(1) with { ArchetypeName = "The Strategic Leader" }, None);

        var card = await _h.Launchpad.AssessmentAsync(_h.Me, None);
        Assert.Equal("done", card.Status);
        Assert.Equal("The People-First Service Builder", card.Archetype!.Name);
        Assert.Equal("🤝", card.Archetype.Emoji);
        Assert.Equal(new[] { "People", "Helping", "Structure" }, card.Archetype.Traits);
        Assert.Equal(2, card.MandatoryCourses);

        var sectors = await _h.Launchpad.SectorsAsync(_h.Me, None);
        Assert.Equal(94, sectors[0].Fit);
        Assert.Equal(sectors.OrderByDescending(s => s.Fit).Select(s => s.Id), sectors.Select(s => s.Id));
    }

    [Fact]
    public async Task Profile_and_jobs_cards()
    {
        _h.Learner.Progress = ProgressLookup.Unavailable;
        Assert.Equal(CareersErrors.LearnerUnavailable, (await _h.Launchpad.ProfileAsync(_h.Me, None)).Error);

        _h.Learner.Progress = FakeLearnerDirectory.Percent(60);
        var profile = (await _h.Launchpad.ProfileAsync(_h.Me, None)).Value;
        Assert.Equal(60, profile.Percent);
        Assert.Equal(new[] { "Hit Me Up" }, profile.Missing);
        Assert.Equal(1, profile.SectionsLeft);

        _h.Learner.Profile = CareersHarness.PuneProfile();
        var jobs = await _h.Launchpad.JobsAsync(_h.Me, None);
        Assert.False(jobs.Locked);
        Assert.True(jobs.Matched > 0);
        Assert.Equal(jobs.Matched, jobs.NewMatches);
        Assert.NotNull(jobs.Top);

        await _h.Jobs.SummaryAsync(_h.Me, None);
        Assert.Equal(0, (await _h.Launchpad.JobsAsync(_h.Me, None)).NewMatches);

        var alerts = await _h.Launchpad.SetFestAlertsAsync(_h.Me, true, None);
        Assert.True(alerts.AlertsOn);
        Assert.Equal("tech-bpo-pune", alerts.Fest!.Id);
        Assert.Equal("In person", alerts.Fest.Mode);
        Assert.True((await _h.Fests.AlertsAsync(_h.Me, None)).Online);
    }

    [Fact]
    public async Task Matches_show_once_published()
    {
        _h.Learner.Profile = CareersHarness.PuneProfile(new LearnerSkill("Customer service", "assessed"), new LearnerSkill("Hindi + English", "level"), new LearnerSkill("Typing", "declared"));

        var hidden = await _h.Matches.BoardAsync(_h.Me, None);
        Assert.Empty(hidden.Jobs);
        Assert.False(hidden.Visible);

        await _h.Matches.PublishAsync(_h.Me, new VisibilitySettings(true, true), None);
        var board = await _h.Matches.BoardAsync(_h.Me, None);
        Assert.True(board.Visible);
        Assert.NotEmpty(board.Jobs);
        Assert.All(board.Jobs, j => Assert.True(j.Match >= 60));
        Assert.Equal("recommended", board.Jobs[0].Status);
        Assert.Empty(board.Industries);

        var chat = board.Jobs.Single(j => j.Id == "m5");
        Assert.Equal("boost", chat.TipKind);
        Assert.Contains("Typing", chat.Tip);

        Assert.True((await _h.Matches.SaveAsync(_h.Me, "m5", true, None)).IsSuccess);
        Assert.True((await _h.Matches.ApplyAsync(_h.Me, "m1", None)).IsSuccess);
        var after = await _h.Matches.BoardAsync(_h.Me, None);
        Assert.Equal("saved", after.Jobs.Single(j => j.Id == "m5").Status);
        Assert.Equal(CareersErrors.MatchNotFound, (await _h.Matches.AcceptAsync(_h.Me, "m9", None)).Error);

        var preview = await _h.Matches.PreviewAsync(_h.Me, None);
        Assert.Equal(board.Total, preview.Count);
        Assert.Equal(25, preview.RadiusKm);
    }

    [Fact]
    public async Task Erasure_removes_rows_and_files()
    {
        var cv = await _h.UploadCvAsync();
        await _h.Jobs.SaveAsync(_h.Me, "job-1", true, None);

        await _h.Erased.HandleAsync(new AccountErased(_h.Me) { TenantId = CareersHarness.Tenant }, None);

        Assert.Empty(_h.Store.CvFiles);
        Assert.Empty(_h.Store.SavedJobs);
        Assert.False(_h.Store.Blobs.ContainsKey(cv.BlobName));
    }
}
