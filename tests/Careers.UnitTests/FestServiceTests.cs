using Careers.Application;
using Careers.Application.Abstractions;
using Careers.Application.Contracts;
using Careers.Domain;
using Careers.UnitTests.Fakes;

namespace Careers.UnitTests;

public sealed class FestServiceTests
{
    private const string Pune = "tech-bpo-pune";
    private const string Day1 = "2026-12-01";

    private static readonly CancellationToken None = CancellationToken.None;

    private readonly CareersHarness _h = new();

    public FestServiceTests()
    {
        _h.Learner.Progress = FakeLearnerDirectory.Percent(60);
    }

    [Fact]
    public async Task Booking_needs_a_60_percent_job_profile()
    {
        _h.Learner.Progress = FakeLearnerDirectory.Percent(45);
        Assert.Equal(CareersErrors.ProfileIncomplete, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00")), None)).Error);

        _h.Learner.Progress = ProgressLookup.Unavailable;
        Assert.Equal(CareersErrors.LearnerUnavailable, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00")), None)).Error);

        _h.Learner.Progress = FakeLearnerDirectory.Percent(45);
        var summary = await _h.Fests.SummaryAsync(_h.Me, None);
        Assert.Equal(45, summary.ProfilePercent);
        Assert.Equal(60, summary.UnlockAt);
        var missing = Assert.Single(summary.Missing);
        Assert.Equal("Hit Me Up · Backup contact", missing.Title);
        Assert.Equal("1 step left", missing.Detail);
        Assert.Equal("/quests/hit-me-up", missing.Link);
        Assert.Equal(4, summary.Upcoming);
    }

    [Fact]
    public async Task Booking_two_employers_gives_one_ticket_and_one_event()
    {
        var booked = await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00"), ("tp", Day1, "11:00")), None);

        Assert.True(booked.IsSuccess);
        Assert.True(booked.Value.Created);
        var ticket = booked.Value.Booking;
        Assert.StartsWith("HF-", ticket.Code);
        Assert.Equal(new[] { "cg", "tp" }, ticket.Interviews.Select(i => i.EmployerId));
        Assert.Equal("Tue 1 Dec", ticket.Interviews[0].DayLabel);
        Assert.Equal("10:00 am", ticket.Interviews[0].Time);
        Assert.Equal("10:00 – 10:30 am", ticket.Interviews[0].Range);
        Assert.Equal("Hall B, Desk 4", ticket.Interviews[0].Desk);
        Assert.Equal(47, ticket.DaysToGo);
        Assert.Equal("Job Profile CV · Creative", ticket.Cv);
        Assert.True(ticket.Fest.Booked);
        Assert.Equal(9, ticket.Fest.SeatsLeft);
        Assert.Equal(5, ticket.Fest.Employers.Single(e => e.Id == "cg").SlotsLeft);
        Assert.IsType<FestBooked>(Assert.Single(_h.Store.Events));

        // Rescheduling one employer keeps the ticket and doesn't publish again.
        var moved = await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("tp", Day1, "12:00")), None);
        Assert.False(moved.Value.Created);
        Assert.Equal("12:00 pm", moved.Value.Booking.Interviews.Single(i => i.EmployerId == "tp").Time);
        Assert.Single(_h.Store.Events);

        var grid = (await _h.Fests.SlotsAsync(Pune, ["cg", "nobody"], None)).Value;
        Assert.Equal(new[] { "cg" }, grid.Slots.Keys);
        Assert.Equal(1, grid.Slots["cg"][Day1].Single(s => s.Time == "10:00").Left);
        Assert.Equal(3, grid.Days.Count);
    }

    [Fact]
    public async Task Slot_rules_follow_br_69()
    {
        Assert.Equal(CareersErrors.SlotOverlap, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00"), ("tp", Day1, "10:30")), None)).Error);
        Assert.Equal(CareersErrors.SlotFull, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "11:00")), None)).Error);
        Assert.Equal(CareersErrors.SlotFull, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cx", Day1, "10:00")), None)).Error);
        Assert.Equal(CareersErrors.SlotNotFound, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", "2026-12-09", "10:00")), None)).Error);
        Assert.Equal(CareersErrors.EmployerNotFound, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("zz", Day1, "10:00")), None)).Error);
        Assert.Equal(CareersErrors.FestFull, (await _h.Fests.BookAsync(_h.Me, "fintech-blr", CareersHarness.Book(("pz", "2027-01-28", "10:00")), None)).Error);

        await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00"), ("tp", "2026-12-02", "10:30")), None);
        Assert.Equal(CareersErrors.TooManyEmployers, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("hl", "2026-12-03", "10:00")), None)).Error);
    }

    [Fact]
    public async Task Cancel_one_interview_then_the_ticket()
    {
        var ticket = (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00"), ("tp", Day1, "11:00")), None)).Value.Booking;

        var left = await _h.Fests.CancelAsync(_h.Me, ticket.Id, "cg", None);
        Assert.Equal(new[] { "tp" }, left.Value!.Interviews.Select(i => i.EmployerId));
        Assert.Equal(CareersErrors.InterviewNotFound, (await _h.Fests.CancelAsync(_h.Me, ticket.Id, "cg", None)).Error);

        var gone = await _h.Fests.CancelAsync(_h.Me, ticket.Id, null, None);
        Assert.True(gone.IsSuccess);
        Assert.Null(gone.Value);
        Assert.Empty(_h.Store.Bookings);
        Assert.IsType<FestCancelled>(_h.Store.Events[^1]);
        Assert.Equal(CareersErrors.BookingNotFound, (await _h.Fests.BookingAsync(_h.Me, ticket.Id, None)).Error);
    }

    [Fact]
    public async Task Cancelling_inside_24_hours_is_refused()
    {
        var ticket = (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("cg", Day1, "10:00")), None)).Value.Booking;

        // 30 Nov, 12:00 IST: 22 hours before the interview.
        _h.Time.SetUtcNow(new DateTimeOffset(2026, 11, 30, 6, 30, 0, TimeSpan.Zero));

        Assert.Equal(CareersErrors.TooLateToCancel, (await _h.Fests.CancelAsync(_h.Me, ticket.Id, null, None)).Error);

        // On the first day booking has closed.
        _h.Time.SetUtcNow(new DateTimeOffset(2026, 12, 1, 3, 0, 0, TimeSpan.Zero));
        Assert.Equal(CareersErrors.BookingClosed, (await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("tp", Day1, "12:00")), None)).Error);
    }

    [Fact]
    public async Task After_the_fest_outcomes_and_rating_and_an_offer_in_my_jobs()
    {
        await _h.Fests.BookAsync(_h.Me, Pune, CareersHarness.Book(("tp", Day1, "11:00")), None);
        Assert.Equal(CareersErrors.PastFestNotFound, (await _h.Fests.RateAsync(_h.Me, Pune, 4, None)).Error);

        _h.Time.SetUtcNow(new DateTimeOffset(2026, 12, 10, 6, 0, 0, TimeSpan.Zero));
        Assert.Empty(await _h.Fests.BookingsAsync(_h.Me, None));
        var past = Assert.Single(await _h.Fests.PastAsync(_h.Me, None));
        Assert.Equal(("01", "DEC", "2026"), (past.Day, past.Month, past.Year));

        var marked = await _h.Fests.OutcomeAsync(_h.Me, Pune, "tp", "offer", None);
        Assert.Equal("offer", marked.Value.Interviews.Single().Outcome);
        await _h.Fests.OutcomeAsync(_h.Me, Pune, "tp", "offer", None);
        var offer = Assert.Single(_h.Store.Applications);
        Assert.Equal(ApplicationStatus.Offer, offer.Status);
        Assert.Equal("Voice Process Exec", offer.Title);
        Assert.Equal("job-1", offer.JobId);
        Assert.Equal(CareersErrors.InterviewNotFound, (await _h.Fests.OutcomeAsync(_h.Me, Pune, "cg", "no", None)).Error);

        Assert.Equal(4, (await _h.Fests.RateAsync(_h.Me, Pune, 4, None)).Value.Rating);
        Assert.Null((await _h.Fests.OutcomeAsync(_h.Me, Pune, "tp", null, None)).Value.Interviews.Single().Outcome);
    }

    [Fact]
    public async Task Waitlist_saves_and_alerts()
    {
        var info = await _h.Fests.JoinWaitlistAsync(_h.Me, "fintech-blr", new WaitlistRequest("pz", true, true), None);
        Assert.Equal(1, info.Value.Position);
        Assert.Equal(1, (await _h.Fests.WaitlistInfoAsync(_h.Me, "fintech-blr", "pz", None)).Value.Position);
        Assert.Equal(1, (await _h.Fests.WaitlistInfoAsync(_h.Me, "fintech-blr", "rz", None)).Value.Position);
        Assert.Equal(CareersErrors.EmployerNotFound, (await _h.Fests.WaitlistInfoAsync(_h.Me, "fintech-blr", "cg", None)).Error);

        Assert.True((await _h.Fests.SaveAsync(_h.Me, "online-cs", true, None)).Value.Saved);
        Assert.Equal(new[] { "online-cs" }, (await _h.Fests.FestsAsync(_h.Me, "online", None)).Select(f => f.Id));
        Assert.Equal(3, (await _h.Fests.FestsAsync(_h.Me, "free", None)).Count);
        Assert.Empty(await _h.Fests.FestsAsync(_h.Me, "city", None));

        Assert.False((await _h.Fests.AlertsAsync(_h.Me, None)).City);
        Assert.True((await _h.Fests.SetAlertsAsync(_h.Me, new FestAlertsDto(true, false), None)).City);
    }
}
