using System.Net;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Testing;

namespace Careers.IntegrationTests;

/// <summary>API-041–056 against MySQL.</summary>
[Collection(CareersApiCollection.Name)]
public sealed class FestEndpointsTests(CareersApiFactory api)
{
    private static object Book(params (string Employer, string Date, string Time)[] picks) => new
    {
        picks = picks.Select(p => new { employerId = p.Employer, date = p.Date, time = p.Time }).ToArray(),
        reminders = new { whatsapp = true, sms = true, calendar = false },
        cvTemplate = "Creative",
    };

    [Fact]
    public async Task Browse_save_and_slots()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);

        var summary = await (await client.GetAsync("/api/v1/fests/summary")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(4, summary.GetProperty("upcoming").GetInt32());
        Assert.Equal(0, summary.GetProperty("profilePercent").GetInt32());
        Assert.Equal(60, summary.GetProperty("unlockAt").GetInt32());

        var all = await (await client.GetAsync("/api/v1/fests?filter=all")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(4, all.GetArrayLength());
        Assert.Equal("tech-bpo-pune", all[0].GetProperty("id").GetString());
        Assert.Equal("2026-12-01", all[0].GetProperty("start").GetString());
        Assert.Equal(1, (await (await client.GetAsync("/api/v1/fests?filter=online")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());

        var fest = await (await client.GetAsync("/api/v1/fests/fintech-blr")).JsonAsync(HttpStatusCode.OK);
        Assert.True(fest.GetProperty("waitlistOpen").GetBoolean());
        Assert.True(fest.GetProperty("employers")[0].GetProperty("full").GetBoolean());
        Assert.Equal("fest-not-found", await (await client.GetAsync("/api/v1/fests/nope")).ProblemCodeAsync(HttpStatusCode.NotFound));

        var saved = await (await client.PutJsonAsync("/api/v1/fests/online-cs/saved", new { saved = true })).JsonAsync(HttpStatusCode.OK);
        Assert.True(saved.GetProperty("saved").GetBoolean());

        var grid = await (await client.GetAsync("/api/v1/fests/tech-bpo-pune/slots?employers=cg,tp")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(3, grid.GetProperty("days").GetArrayLength());
        Assert.Equal("Tue 1 Dec", grid.GetProperty("days")[0].GetProperty("label").GetString());
        Assert.Equal(6, grid.GetProperty("slots").GetProperty("cg").GetProperty("2026-12-01").GetArrayLength());
        Assert.Equal("10:30 am", grid.GetProperty("slots").GetProperty("tp").GetProperty("2026-12-01")[0].GetProperty("label").GetString());
        Assert.Equal(30, grid.GetProperty("minutes").GetInt32());
        Assert.Equal(2, grid.GetProperty("maxEmployers").GetInt32());

        var alerts = await (await client.PutJsonAsync("/api/v1/fests/alerts", new { city = true, online = false })).JsonAsync(HttpStatusCode.OK);
        Assert.True(alerts.GetProperty("city").GetBoolean());
        Assert.False((await (await client.GetAsync("/api/v1/fests/alerts")).JsonAsync(HttpStatusCode.OK)).GetProperty("online").GetBoolean());
    }

    [Fact]
    public async Task Booking_gate_ticket_and_cancel()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);

        Assert.Equal("profile-incomplete", await (await client.PostJsonAsync("/api/v1/fests/retail-mumbai/book", Book(("rt", "2026-12-20", "11:00")))).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        api.Learners.SetPercent(me, 61);
        var created = await client.PostJsonAsync("/api/v1/fests/retail-mumbai/book", Book(("rt", "2026-12-20", "11:00")));
        var ticket = await created.JsonAsync(HttpStatusCode.Created);
        var id = ticket.GetProperty("id").GetString();
        Assert.Equal($"/api/v1/fests/bookings/{id}", created.Headers.Location?.ToString());
        Assert.Equal("Hall 3, Desk 2", ticket.GetProperty("interviews")[0].GetProperty("desk").GetString());
        Assert.Equal(66, ticket.GetProperty("daysToGo").GetInt32());
        Assert.True(ticket.GetProperty("reminders").GetProperty("whatsapp").GetBoolean());
        Assert.Equal(JsonValueKindNull, ticket.GetProperty("live").ValueKind);

        var added = await (await client.PostJsonAsync("/api/v1/fests/retail-mumbai/book", Book(("dm", "2026-12-20", "16:00")))).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, added.GetProperty("interviews").GetArrayLength());
        Assert.Equal("slot-overlap", await (await client.PostJsonAsync("/api/v1/fests/retail-mumbai/book", Book(("dm", "2026-12-20", "11:00")))).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("too-many-employers", await (await client.PostJsonAsync("/api/v1/fests/retail-mumbai/book", Book(("lf", "2026-12-20", "16:00")))).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/fests/retail-mumbai/book", Book(("lf", "20 Dec", "4pm")))).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        var bookings = await (await client.GetAsync("/api/v1/fests/bookings")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, bookings.GetArrayLength());
        var one = await (await client.GetAsync($"/api/v1/fests/bookings/{id}")).JsonAsync(HttpStatusCode.OK);
        Assert.True(one.GetProperty("fest").GetProperty("booked").GetBoolean());

        var left = await (await client.PostJsonAsync($"/api/v1/fests/bookings/{id}/cancel", new { employerId = "dm", reason = "Clash" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, left.GetProperty("interviews").GetArrayLength());
        var gone = await client.PostJsonAsync($"/api/v1/fests/bookings/{id}/cancel", new { employerId = (string?)null, reason = "Can't make it" });
        Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode);
        Assert.Equal("booking-not-found", await (await client.GetAsync($"/api/v1/fests/bookings/{id}")).ProblemCodeAsync(HttpStatusCode.NotFound));

        var events = await api.QueryAsync(db => db.OutboxMessages
            .Where(m => (m.EventType == "careers.fest.booked" || m.EventType == "careers.fest.cancelled") && m.Payload.Contains(me.ToString()))
            .Select(m => m.EventType)
            .ToListAsync());
        Assert.Equal(new[] { "careers.fest.booked", "careers.fest.cancelled" }, events.Order());
    }

    [Fact]
    public async Task Waitlist_and_full_fests()
    {
        var me = Guid.NewGuid();
        api.Learners.SetPercent(me, 100);
        var client = api.ClientFor(me);

        Assert.Equal("fest-full", await (await client.PostJsonAsync("/api/v1/fests/fintech-blr/book", Book(("pz", "2027-01-28", "10:00")))).ProblemCodeAsync(HttpStatusCode.Conflict));

        var before = await (await client.GetAsync("/api/v1/fests/fintech-blr/waitlist?employerId=rz")).JsonAsync(HttpStatusCode.OK);
        var joined = await (await client.PostJsonAsync("/api/v1/fests/fintech-blr/waitlist", new { employerId = "rz", auto = true, whatsapp = false })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(before.GetProperty("position").GetInt32(), joined.GetProperty("position").GetInt32());
        Assert.Equal("Usually 1 in 3 waitlisted learners get a slot", joined.GetProperty("odds").GetString());
        Assert.Equal("employer-not-found", await (await client.GetAsync("/api/v1/fests/fintech-blr/waitlist?employerId=cg")).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task After_the_fest_outcomes_ratings_and_the_offer_in_my_jobs()
    {
        var me = Guid.NewGuid();
        api.Learners.SetPercent(me, 80);
        var client = api.ClientFor(me);
        await (await client.PostJsonAsync("/api/v1/fests/tech-bpo-pune/book", Book(("hl", "2026-12-02", "10:00")))).JsonAsync(HttpStatusCode.Created);
        Assert.Equal(0, (await (await client.GetAsync("/api/v1/fests/past")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());
        Assert.Equal("past-fest-not-found", await (await client.PutJsonAsync("/api/v1/fests/past/tech-bpo-pune/rating", new { stars = 4 })).ProblemCodeAsync(HttpStatusCode.NotFound));

        api.Clock.SetUtcNow(new DateTimeOffset(2026, 12, 8, 6, 0, 0, TimeSpan.Zero));
        try
        {
            var past = await (await client.GetAsync("/api/v1/fests/past")).JsonAsync(HttpStatusCode.OK);
            Assert.Equal("DEC", past[0].GetProperty("month").GetString());

            var marked = await (await client.PutJsonAsync("/api/v1/fests/past/tech-bpo-pune/outcome", new { employerId = "hl", outcome = "offer" })).JsonAsync(HttpStatusCode.OK);
            Assert.Equal("offer", marked.GetProperty("interviews")[0].GetProperty("outcome").GetString());
            var rated = await (await client.PutJsonAsync("/api/v1/fests/past/tech-bpo-pune/rating", new { stars = 5 })).JsonAsync(HttpStatusCode.OK);
            Assert.Equal(5, rated.GetProperty("rating").GetInt32());
            Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/fests/past/tech-bpo-pune/rating", new { stars = 9 })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

            var applied = await (await client.GetAsync("/api/v1/jobs/applications")).JsonAsync(HttpStatusCode.OK);
            var offer = applied.GetProperty("items")[0];
            Assert.Equal("offer", offer.GetProperty("status").GetString());
            Assert.Equal("Inbound Sales Exec", offer.GetProperty("job").GetProperty("title").GetString());
            Assert.Equal("Offer from Hire Fest", offer.GetProperty("updated").GetString());
        }
        finally
        {
            api.Clock.SetUtcNow(CareersApiFactory.Today);
        }
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;
}
