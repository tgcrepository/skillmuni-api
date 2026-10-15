namespace Careers.Application.Catalogue;

// Catalogue content (jobs, employers, fests, sectors, match cards). There is no employer/content admin in Phase 1,
// so the seed copied from the frontend mocks is served through the provider interfaces below; a CMS or the employer
// portal can replace the providers without touching the use cases.

/// <summary>A job in the My Jobs catalogue (jobs.data.ts). Pay is a display string (A-221); PayIndex is annual lakh.</summary>
public sealed record CatalogueJob(
    string Id,
    string Title,
    string Company,
    string Initials,
    string City,
    string State,
    string Place,
    string Sector,
    string Role,
    string WorkMode,
    string JobType,
    string JobTypeLabel,
    string Pay,
    double PayIndex,
    int PostedDays,
    int Applicants,
    string ApplyBy,
    int? ClosingInDays,
    bool Closed,
    string Experience,
    int Openings,
    string Shift,
    string About,
    IReadOnlyList<string> Duties,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> Skills,
    string CompanyAbout)
{
    public bool IsNew => PostedDays <= 2;

    public bool Remote => WorkMode == "remote";
}

/// <summary>An employer's questions in the apply dialog.</summary>
public sealed record ApplyQuestion(string Id, string Text, string Kind);

public interface IJobCatalogue
{
    IReadOnlyList<CatalogueJob> Jobs { get; }

    /// <summary>Sector → roles, in display order (Role depends on Sector).</summary>
    IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> Sectors { get; }

    /// <summary>State → cities, in display order (City depends on State).</summary>
    IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> States { get; }

    CatalogueJob? Find(string id);

    IReadOnlyList<ApplyQuestion> QuestionsFor(string jobId);
}

public sealed record FestEmployerSeed(string Id, string Company, string Initials, string Role, int Openings, int SlotsLeft, string Days, int Match, string Desk);

/// <summary>A Hire Fest (fest.handlers.ts). Dates are the fest's local calendar days.</summary>
public sealed record CatalogueFest(
    string Id,
    string Name,
    string Mode,
    string City,
    DateOnly Start,
    DateOnly End,
    string DateLabel,
    string Hours,
    string Venue,
    string VenueNote,
    string Hall,
    string? Distance,
    string Organiser,
    IReadOnlyList<FestEmployerSeed> Employers,
    int EmployerCount,
    int RoleCount,
    string Price,
    bool Free,
    int SeatsTotal,
    int SeatsLeft,
    int MatchingRoles,
    string BookingCloses,
    string CancelPolicy,
    IReadOnlyList<string> BeforeYouGo,
    bool WaitlistOpen)
{
    public bool Online => Mode == "online";

    public IEnumerable<DateOnly> Days()
    {
        for (var d = Start; d <= End; d = d.AddDays(1))
        {
            yield return d;
        }
    }
}

/// <summary>An interview slot before bookings are counted: the time and its seats.</summary>
public sealed record SlotSeed(string Time, int Seats);

public interface IFestCatalogue
{
    IReadOnlyList<CatalogueFest> Fests { get; }

    CatalogueFest? Find(string id);

    /// <summary>The employer's slot times on a day and the seats each has.</summary>
    IReadOnlyList<SlotSeed> Slots(string festId, string employerId, DateOnly date);
}

/// <summary>A Placement Zone sector (launchpad.handlers.ts). Fit applies once the assessment is done (BR-63).</summary>
public sealed record SectorSeed(string Id, string Name, string Emoji, int Jobs, int NewJobs, int Fit);

/// <summary>A My Matches card for one market (matches.handlers.ts); the match % is computed per learner.</summary>
public sealed record MatchSeed(
    string Id,
    string Title,
    string Employer,
    string Kind,
    string Location,
    string City,
    string State,
    string Pay,
    string Industry,
    bool Remote,
    IReadOnlyList<string> Skills);

public sealed record IndustrySeed(string Id, string Name, int Fit, string Why, string Roles, string Path, bool PathDone, int Jobs);

public interface ICareerCatalogue
{
    IReadOnlyList<SectorSeed> Sectors { get; }

    IReadOnlyList<MatchSeed> MatchesFor(string market);

    IReadOnlyList<IndustrySeed> Industries { get; }
}
