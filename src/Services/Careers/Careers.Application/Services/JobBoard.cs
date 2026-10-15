using Careers.Application.Catalogue;
using Careers.Application.Contracts;
using Careers.Domain;
using Microsoft.Extensions.Options;

namespace Careers.Application.Services;

/// <summary>A catalogue job as this learner sees it: bookmark, latest live application and match.</summary>
public sealed record JobView(CatalogueJob Job, DateTime? SavedAt, JobApplication? Application, MatchResult? Match)
{
    public bool Saved => SavedAt is not null;

    public int MatchPercent => Match?.Percent ?? 0;
}

/// <summary>Builds job, application and invitation views and DTOs for one learner (jobs.api.ts shapes).</summary>
public sealed class JobBoard(IJobCatalogue catalogue, JobMatcher matcher, CareerClock clock, IOptions<CareersOptions> options)
{
    private static readonly string[] Steps = ["Applied", "Viewed by recruiter", "Shortlisted", "Interview", "Offer"];

    public int Threshold => options.Value.MatchingThreshold;

    public IReadOnlyList<JobView> Views(CareerState state)
    {
        var profile = state.MatchProfile;
        var latest = LatestActive(state.Applications);
        return catalogue.Jobs.Select(j => View(j, state, profile, latest)).ToList();
    }

    public JobView View(CareerState state, CatalogueJob job) => View(job, state, state.MatchProfile, LatestActive(state.Applications));

    public JobDto Dto(JobView view, string? titleOverride = null)
    {
        var j = view.Job;
        return new JobDto(
            j.Id,
            titleOverride ?? j.Title,
            j.Company,
            j.Initials,
            j.City,
            j.State,
            j.Place,
            j.Sector,
            j.Role,
            j.WorkMode,
            j.JobType,
            j.JobTypeLabel,
            j.Pay,
            j.PostedDays,
            j.Applicants,
            j.ApplyBy,
            j.ClosingInDays,
            j.Closed,
            j.IsNew,
            view.Saved,
            view.SavedAt is { } savedAt ? Math.Max(0, clock.DaysSince(savedAt)) : null,
            view.Application is { } app ? clock.Short(app.AppliedAt) : null,
            j.Experience,
            j.Openings,
            j.Shift,
            j.About,
            j.Duties,
            j.Requirements,
            j.Skills,
            j.CompanyAbout,
            view.Match?.Info);
    }

    public ApplicationDto Application(JobApplication app, JobView view)
    {
        var applied = clock.Short(app.AppliedAt);
        var timeline = app.Status switch
        {
            ApplicationStatus.Withdrawn => Timeline(app.Status, [applied, clock.Short(app.WithdrawnAt ?? app.UpdatedAt)]),
            ApplicationStatus.Offer => Timeline(app.Status, [applied, applied, applied, applied, clock.Short(app.UpdatedAt)]),
            _ => Timeline(app.Status, [applied]),
        };
        var similar = catalogue.Jobs.Count(j => j.Role == view.Job.Role && j.Id != view.Job.Id);
        var job = Dto(view with { Application = app.Active ? app : view.Application }, app.Title);
        return new ApplicationDto(
            app.Id.ToString(),
            job,
            applied,
            Updated(app),
            app.CvName,
            app.Status,
            timeline,
            null,
            app.Answers.Select(a => new AnswerDto(a.Q, a.A)).ToList(),
            similar);
    }

    public InvitationDto Invitation(Invitation invitation, JobView view) => new(
        invitation.Id.ToString(),
        Dto(view),
        new RecruiterDto(invitation.RecruiterName, invitation.RecruiterInitials, invitation.RecruiterTitle),
        invitation.Type,
        invitation.InterviewAt is { } at ? clock.Local(at) : null,
        invitation.Message,
        CareerClock.Short(invitation.RespondBy),
        Math.Max(0, clock.DaysUntil(invitation.RespondBy)),
        invitation.StatusOn(clock.Today));

    /// <summary>The application timeline (jobs.handlers.ts <c>timeline</c>).</summary>
    public static IReadOnlyList<TimelineStepDto> Timeline(string status, IReadOnlyList<string> dates)
    {
        string? On(int i) => i < dates.Count ? dates[i] : null;
        if (status == ApplicationStatus.Withdrawn)
        {
            return [new("Applied", On(0), "done"), new("Withdrawn", On(1), "failed")];
        }

        if (status == ApplicationStatus.NotSelected)
        {
            return [new("Applied", On(0), "done"), new("Viewed by recruiter", On(1), "done"), new("Not selected", On(2), "failed")];
        }

        var at = status switch
        {
            ApplicationStatus.Viewed => 1,
            ApplicationStatus.Shortlisted => 2,
            ApplicationStatus.Interview => 3,
            ApplicationStatus.Offer => 4,
            _ => 0,
        };
        return Steps.Select((label, i) => new TimelineStepDto(
            label,
            On(i),
            i < at ? "done" : i == at ? (i == 3 ? "current" : "done") : "todo")).ToList();
    }

    private string Updated(JobApplication app)
    {
        if (app.Status == ApplicationStatus.Withdrawn)
        {
            return $"You withdrew on {clock.Short(app.WithdrawnAt ?? app.UpdatedAt)}";
        }

        if (app.Source == ApplicationSource.FestOffer)
        {
            return "Offer from Hire Fest";
        }

        return clock.DaysSince(app.UpdatedAt) switch
        {
            <= 0 => "Updated today",
            1 => "Updated yesterday",
            var days => $"Updated {days} days ago",
        };
    }

    private JobView View(CatalogueJob job, CareerState state, MatchProfile? profile, IReadOnlyDictionary<string, JobApplication> latest) => new(
        job,
        state.Saved.TryGetValue(job.Id, out var savedAt) ? savedAt : null,
        latest.GetValueOrDefault(job.Id),
        profile is null ? null : matcher.Score(job.Skills, job.City, job.State, job.Remote, profile));

    private static Dictionary<string, JobApplication> LatestActive(IReadOnlyList<JobApplication> applications) =>
        applications
            .Where(a => a.Active)
            .GroupBy(a => a.JobId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AppliedAt).First(), StringComparer.Ordinal);
}
