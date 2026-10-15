using Careers.Application;
using Careers.Application.Abstractions;
using Careers.Application.Contracts;
using Careers.Application.Handlers;
using Careers.Application.Services;
using Careers.Domain;
using Careers.Infrastructure.Catalogue;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Careers.UnitTests.Fakes;

/// <summary>Every Careers use case wired to in-memory fakes, the seed catalogue and a controllable clock (15 Oct 2026).</summary>
internal sealed class CareersHarness
{
    public const string Tenant = "skillmuni-in";

    public CareersHarness()
    {
        var options = MsOptions.Create(Settings);
        Clock = new CareerClock(Time, TenantContext, options);
        Matcher = new JobMatcher(options);
        States = new CareerStateLoader(Store, Store, Store, Learner, TenantContext, Clock);
        Board = new JobBoard(JobCatalogue, Matcher, Clock, options);
        FestViews = new FestViews(FestCatalogue, Clock, options);
        Jobs = new JobsService(JobCatalogue, States, Board, Store, Store, Store, Store, Learner, Store, Store, Store, TenantContext, Clock, options, NullLogger<JobsService>.Instance);
        Fests = new FestService(FestCatalogue, JobCatalogue, FestViews, States, Store, Store, Store, Learner, Store, Store, TenantContext, Clock, options, NullLogger<FestService>.Instance);
        Launchpad = new LaunchpadService(States, Board, FestViews, FestCatalogue, CareerCatalogue, Store, Store, Learner, Store, Clock, options);
        Matches = new MatchesService(CareerCatalogue, Matcher, States, Store, Learner, Store, TenantContext, Clock, options);
        Erased = new AccountErasedHandler(Store, Store, Store, NullLogger<AccountErasedHandler>.Instance);
        Assessment = new AssessmentSubmittedHandler(States, Clock);
        JobProfile = new JobProfileCompletedHandler(States, Clock);
    }

    public CareersOptions Settings { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 15, 6, 0, 0, TimeSpan.Zero));

    public InMemoryCareersStore Store { get; } = new();

    public FakeLearnerDirectory Learner { get; } = new();

    public FixedTenant TenantContext { get; } = new();

    public SeedJobCatalogue JobCatalogue { get; } = new();

    public SeedFestCatalogue FestCatalogue { get; } = new();

    public SeedCareerCatalogue CareerCatalogue { get; } = new();

    public CareerClock Clock { get; }

    public JobMatcher Matcher { get; }

    public CareerStateLoader States { get; }

    public JobBoard Board { get; }

    public FestViews FestViews { get; }

    public JobsService Jobs { get; }

    public FestService Fests { get; }

    public LaunchpadService Launchpad { get; }

    public MatchesService Matches { get; }

    public AccountErasedHandler Erased { get; }

    public AssessmentSubmittedHandler Assessment { get; }

    public JobProfileCompletedHandler JobProfile { get; }

    public Guid Me { get; } = Guid.NewGuid();

    /// <summary>A learner in Pune with a few skills, so jobs score.</summary>
    public static LearnerCareerProfile PuneProfile(params LearnerSkill[] skills) =>
        new("Yash Pohare", "+91 98•••• 0697", "Pune", "Maharashtra", false, skills.Length > 0 ? skills : [new("Communication", "verified"), new("Customer service", "assessed"), new("English", "declared")]);

    public async Task<CvFile> UploadCvAsync()
    {
        using var pdf = new MemoryStream("%PDF-1.7 test"u8.ToArray());
        var uploaded = await Jobs.UploadCvAsync(Me, new UploadedFile("Yash_CV.pdf", pdf.Length, pdf), CancellationToken.None);
        Assert.True(uploaded.IsSuccess);
        return Store.CvFiles.Single(f => f.Id.ToString() == uploaded.Value.Id);
    }

    public Invitation Invite(string jobId, string type = "apply", int respondInDays = 7)
    {
        var invitation = Invitation.Create(Tenant, Me, jobId, "Priya S.", "PS", "Recruiter", type, type == "interview" ? Time.GetUtcNow().UtcDateTime.AddDays(3) : null, "Hi", Clock.Today.AddDays(respondInDays), Time.GetUtcNow().UtcDateTime);
        Store.Invitations.Add(invitation);
        return invitation;
    }

    public static BookRequest Book(params (string Employer, string Date, string Time)[] picks) =>
        new(picks.Select(p => new BookPickDto(p.Employer, p.Date, p.Time)).ToList(), new RemindersDto(true, false, true), "Creative");

    public sealed class FixedTenant : ITenantContext
    {
        public string TenantId => Tenant;

        public string Market { get; set; } = "IN";
    }
}
