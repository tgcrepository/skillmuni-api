using Careers.Infrastructure.Catalogue;

namespace Careers.UnitTests;

/// <summary>The seed must equal the frontend mock (values taken by running jobs.data.ts and fest.handlers.ts).</summary>
public sealed class CatalogueTests
{
    private readonly SeedJobCatalogue _jobs = new();

    [Fact]
    public void Job_catalogue_has_the_mocks_128_jobs()
    {
        Assert.Equal(128, _jobs.Jobs.Count);
        Assert.Equal(20, _jobs.Jobs.Count(j => j.WorkMode == "remote"));
        Assert.Equal(46, _jobs.Jobs.Count(j => j.City == "Pune"));
        Assert.Equal(24, _jobs.Jobs.Count(j => j.JobType == "learnership"));
        Assert.Equal(11, _jobs.Jobs.Count(j => j.ClosingInDays is not null));
        Assert.Equal(9, _jobs.Jobs.Count(j => j.IsNew));
        Assert.Equal(14, _jobs.Jobs.Count(j => j.Role == "Customer service"));
    }

    [Theory]
    [InlineData("job-1", "Customer Service Associate", "Teleperformance", "Pune", "Pune, Maharashtra", "full-time", "₹2.4–3.0 LPA", 2.4, 2, 32, 3)]
    [InlineData("job-3", "Technical Support Trainee", "Concentrix", "Bengaluru", "Bengaluru, Karnataka", "learnership", "₹15,000 / month stipend", 1.8, 1, 42, 2)]
    [InlineData("job-5", "Data Entry Operator", "Tata AIG", "Remote", "Remote · India", "contract", "₹2.2 LPA", 2.2, 3, 81, 15)]
    [InlineData("job-8", "Sales Executive", "Airtel", "Goa", "Goa", "full-time", "₹3.3–3.6 LPA", 3.3, 38, 49, 2)]
    [InlineData("job-9", "Chat Support Executive", "Teleperformance", "New Delhi", "New Delhi, Delhi", "part-time", "₹3.7–4.4 LPA", 3.7, 40, 47, 3)]
    [InlineData("job-42", "Back Office Executive", "Axis Bank", "Pune", "Pune, Maharashtra", "full-time", "₹3.5–4.5 LPA", 3.5, 7, 83, 10)]
    [InlineData("job-60", "Sales Executive", "Airtel", "Remote", "Remote · India", "full-time", "₹1.9–2.9 LPA", 1.9, 10, 18, 2)]
    [InlineData("job-61", "Claims Processing Associate", "Tata AIG", "Remote", "Remote · India", "learnership", "₹12,000 / month stipend", 1.44, 5, 24, 14)]
    [InlineData("job-100", "Back Office Executive", "Axis Bank", "Nagpur", "Nagpur, Maharashtra", "learnership", "₹12,000 / month stipend", 1.44, 29, 34, 14)]
    [InlineData("job-128", "Customer Service Associate", "Airtel", "Pune", "Pune, Maharashtra", "part-time", "₹2.6–3.1 LPA", 2.6, 7, 72, 13)]
    public void Generated_jobs_match_the_mock(string id, string title, string company, string city, string place, string type, string pay, double payIndex, int posted, int applicants, int openings)
    {
        var job = _jobs.Find(id)!;
        Assert.Equal(title, job.Title);
        Assert.Equal(company, job.Company);
        Assert.Equal(city, job.City);
        Assert.Equal(place, job.Place);
        Assert.Equal(type, job.JobType);
        Assert.Equal(pay, job.Pay);
        Assert.Equal(payIndex, job.PayIndex, 6);
        Assert.Equal(posted, job.PostedDays);
        Assert.Equal(applicants, job.Applicants);
        Assert.Equal(openings, job.Openings);
    }

    [Fact]
    public void Job_61_closes_soon_and_job_5_is_a_six_month_contract()
    {
        Assert.Equal(5, _jobs.Find("job-61")!.ClosingInDays);
        Assert.Equal("Contract · 6 months", _jobs.Find("job-5")!.JobTypeLabel);
        Assert.Equal(string.Empty, _jobs.Find("job-5")!.State);
    }

    [Fact]
    public void Fest_seats_follow_the_mock()
    {
        Assert.Equal(0, SeedFestCatalogue.Seats("online-cs", "hl", "2027-01-14", "10:00"));
        Assert.Equal(4, SeedFestCatalogue.Seats("retail-mumbai", "rt", "2026-12-20", "11:00"));
        Assert.Equal(2, SeedFestCatalogue.Seats("tech-bpo-pune", "hl", "2026-12-02", "12:30"));

        var fests = new SeedFestCatalogue();
        var day1 = fests.Slots("tech-bpo-pune", "cg", new DateOnly(2026, 12, 1));
        Assert.Equal(new[] { "10:00", "10:30", "11:00", "11:30", "14:00", "14:30" }, day1.Select(s => s.Time));
        Assert.Equal(new[] { 2, 3, 0, 4, 3, 6 }, day1.Select(s => s.Seats));
        Assert.Equal(4, fests.Fests.Count);
    }

    [Fact]
    public void Sectors_and_matches_per_market()
    {
        var career = new SeedCareerCatalogue();
        Assert.Equal(14, career.Sectors.Count);
        Assert.Equal(5, career.MatchesFor("IN").Count);
        Assert.Equal(6, career.MatchesFor("ZA").Count);
    }
}
