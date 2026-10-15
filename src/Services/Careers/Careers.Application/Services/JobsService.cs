using System.Globalization;
using Careers.Application.Abstractions;
using Careers.Application.Catalogue;
using Careers.Application.Contracts;
using Careers.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Careers.Application.Services;

/// <summary>API-075–089: My Jobs v2 (Figma 272:22353). Catalogue from <see cref="IJobCatalogue"/>, the learner's state from MySQL.</summary>
public interface IJobsService
{
    Task<JobsSummaryDto> SummaryAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result<ListResultDto<JobDto>>> JobsAsync(Guid learnerId, string tab, ListQuery query, CancellationToken cancellationToken);

    Task<ListResultDto<InvitationDto>> InvitationsAsync(Guid learnerId, ListQuery query, CancellationToken cancellationToken);

    Task<ListResultDto<ApplicationDto>> ApplicationsAsync(Guid learnerId, ListQuery query, CancellationToken cancellationToken);

    Task<Result<JobDto>> JobAsync(Guid learnerId, string jobId, CancellationToken cancellationToken);

    Task<Result<InvitationDto>> InvitationAsync(Guid learnerId, string invitationId, CancellationToken cancellationToken);

    Task<Result<ApplicationDto>> ApplicationAsync(Guid learnerId, string applicationId, CancellationToken cancellationToken);

    Task<Result<JobDto>> SaveAsync(Guid learnerId, string jobId, bool saved, CancellationToken cancellationToken);

    Task<Result<ApplyInfoDto>> ApplyInfoAsync(Guid learnerId, string jobId, string displayName, CancellationToken cancellationToken);

    Task<Result<CvOptionDto>> UploadCvAsync(Guid learnerId, UploadedFile? file, CancellationToken cancellationToken);

    Task<Result<ApplicationDto>> ApplyAsync(Guid learnerId, string jobId, ApplyRequest request, CancellationToken cancellationToken);

    Task<Result<InvitationDto>> RespondAsync(Guid learnerId, string invitationId, string action, string? note, CancellationToken cancellationToken);

    Task<Result<ApplicationDto>> WithdrawAsync(Guid learnerId, string applicationId, CancellationToken cancellationToken);

    Task<AlertSettingsDto> AlertsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<AlertSettingsDto> SetAlertsAsync(Guid learnerId, AlertSettingsDto alerts, CancellationToken cancellationToken);
}

public sealed partial class JobsService(
    IJobCatalogue catalogue,
    CareerStateLoader states,
    JobBoard board,
    ISavedJobRepository savedJobs,
    IApplicationRepository applications,
    ICvFileRepository cvFiles,
    IInvitationRepository invitations,
    ILearnerDirectory directory,
    IBlobStore blobs,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    CareerClock clock,
    IOptions<CareersOptions> options,
    ILogger<JobsService> logger) : IJobsService
{
    public const string Available = "available";
    public const string Matching = "matching";
    public const string Bookmarked = "bookmarked";

    private static readonly string[] PayLevels = ["1", "2", "3", "5"];
    private static readonly string[] PostedWithin = ["1", "7", "30"];
    private static readonly string[] MatchLevels = ["60", "70", "80", "90"];
    private static readonly string[] AppliedWithin = ["7", "30", "90"];

    private CareersOptions Rules => options.Value;

    public async Task<JobsSummaryDto> SummaryAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var progressTask = directory.ProgressAsync(learnerId, cancellationToken);
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        var progress = (await progressTask).Progress;
        var views = board.Views(state);
        var invites = await invitations.ListAsync(learnerId, cancellationToken);
        var today = clock.Today;

        // BR-62: opening My Jobs clears the Launchpad's "new matches".
        var learner = await states.LearnerForUpdateAsync(learnerId, cancellationToken);
        learner.SawJobs(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new JobsSummaryDto(
            catalogue.Jobs.Count,
            views.Count(v => v.MatchPercent >= board.Threshold),
            invites.Count(i => catalogue.Find(i.JobId) is not null),
            invites.Count(i => catalogue.Find(i.JobId) is not null && i.StatusOn(today) == InvitationStatus.New),
            views.Count(v => v.Saved),
            state.Applications.Count,
            progress.Percent,

            // A-261: My Jobs is never locked.
            0,
            progress.WithGaps.Select(q => q.Name).ToList(),
            Rules.MinutesToUnlock,
            state.Profile?.City is { Length: > 0 } city ? $"{city} and Remote" : string.Empty);
    }

    public async Task<Result<ListResultDto<JobDto>>> JobsAsync(Guid learnerId, string tab, ListQuery query, CancellationToken cancellationToken)
    {
        if (tab is not (Available or Matching or Bookmarked))
        {
            return CareersErrors.InvalidTab;
        }

        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        IEnumerable<JobView> pool = board.Views(state);
        if (tab == Matching)
        {
            pool = pool.Where(v => v.MatchPercent >= board.Threshold);
        }
        else if (tab == Bookmarked)
        {
            pool = pool.Where(v => v.Saved);
        }

        // The place box is a shortcut for Location: once State or City is picked, the filter wins.
        var byLocation = query.Filter("state").Count > 0 || query.Filter("city").Count > 0;
        var items = ListEngine.Text(pool, query.Q, byLocation ? string.Empty : query.Place, v => v.Job);
        var hits = Sort(
            items.Where(v => ListEngine.Passes(v, query.Filters, JobValues)).ToList(),
            query.Sort.Length > 0 ? query.Sort : tab == Matching ? "match" : tab == Bookmarked ? "saved" : "relevant");

        string? note = null;
        if (tab == Bookmarked)
        {
            var closing = hits.Count(v => v.Job.ClosingInDays is not null);
            note = $"{ListEngine.Plural(hits.Count, "saved job")}{(closing > 0 ? $" · {closing} closing soon" : string.Empty)}";
        }

        return ListEngine.List(
            hits.Select(v => board.Dto(v)).ToList(),
            query.Limit,
            JobFacets(items, query, tab),
            ListEngine.Suggestions(items, query.Filters, JobValues),
            note);
    }

    public async Task<ListResultDto<InvitationDto>> InvitationsAsync(Guid learnerId, ListQuery query, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        var today = clock.Today;
        var rows = (await invitations.ListAsync(learnerId, cancellationToken))
            .Select(i => (Invitation: i, Job: catalogue.Find(i.JobId)))
            .Where(x => x.Job is not null)
            .Select(x => (x.Invitation, View: board.View(state, x.Job!)))
            .ToList();

        IReadOnlyList<string>? Get((Invitation Invitation, JobView View) row, string key) => key switch
        {
            "status" => [row.Invitation.StatusOn(today)],
            "inviteType" => [row.Invitation.Type],
            "company" => [row.View.Job.Company],
            _ => null,
        };

        var pool = ListEngine.Text(rows, query.Q, string.Empty, r => r.View.Job);
        var filtered = pool.Where(r => ListEngine.Passes(r, query.Filters, Get));
        var hits = (query.Sort == "newest"
                ? filtered.OrderByDescending(r => r.Invitation.CreatedAt)
                : filtered.OrderBy(r => r.Invitation.StatusOn(today) == InvitationStatus.New ? 0 : 1).ThenBy(r => clock.DaysUntil(r.Invitation.RespondBy)))
            .ToList();
        var facets = new[]
        {
            ListEngine.Facet(pool, query.Filters, Get, "status", [InvitationStatus.New, InvitationStatus.Accepted, InvitationStatus.Declined, InvitationStatus.Expired]),
            ListEngine.Facet(pool, query.Filters, Get, "inviteType", ["apply", "interview"]),
            ListEngine.Facet(pool, query.Filters, Get, "company", pool.Select(r => r.View.Job.Company).Distinct()),
        };
        var fresh = hits.Count(r => r.Invitation.StatusOn(today) == InvitationStatus.New);
        return ListEngine.List(
            hits.Select(r => board.Invitation(r.Invitation, r.View)).ToList(),
            query.Limit,
            facets,
            ListEngine.Suggestions(pool, query.Filters, Get),
            $"{fresh} new · {hits.Count} total");
    }

    public async Task<ListResultDto<ApplicationDto>> ApplicationsAsync(Guid learnerId, ListQuery query, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        var rows = state.Applications
            .Select(a => (Application: a, Job: catalogue.Find(a.JobId)))
            .Where(x => x.Job is not null)
            .Select(x => (x.Application, View: board.View(state, x.Job!)))
            .ToList();

        IReadOnlyList<string>? Get((JobApplication Application, JobView View) row, string key) => key switch
        {
            "status" => [row.Application.Status],
            "sector" => [row.View.Job.Sector],
            "company" => [row.View.Job.Company],
            "dateApplied" => AppliedWithin.Where(d => clock.DaysSince(row.Application.AppliedAt) <= int.Parse(d, CultureInfo.InvariantCulture)).ToList(),
            _ => null,
        };

        var pool = ListEngine.Text(rows, query.Q, string.Empty, r => r.View.Job);
        var hits = pool.Where(r => ListEngine.Passes(r, query.Filters, Get)).ToList();
        var facets = new[]
        {
            ListEngine.Facet(pool, query.Filters, Get, "status", ApplicationStatus.All),
            ListEngine.Facet(pool, query.Filters, Get, "dateApplied", AppliedWithin),
            ListEngine.Facet(pool, query.Filters, Get, "sector", pool.Select(r => r.View.Job.Sector).Distinct()),
            ListEngine.Facet(pool, query.Filters, Get, "company", pool.Select(r => r.View.Job.Company).Distinct()),
        };
        var upcoming = hits.Count(r => r.Application.Status == ApplicationStatus.Interview);
        var note = $"{ListEngine.Plural(hits.Count, "application")}{(upcoming > 0 ? $" · {upcoming} interview coming up" : string.Empty)}";
        return ListEngine.List(
            hits.Select(r => board.Application(r.Application, r.View)).ToList(),
            query.Limit,
            facets,
            ListEngine.Suggestions(pool, query.Filters, Get),
            note);
    }

    public async Task<Result<JobDto>> JobAsync(Guid learnerId, string jobId, CancellationToken cancellationToken)
    {
        if (catalogue.Find(jobId) is not { } job)
        {
            return CareersErrors.JobNotFound;
        }

        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Dto(board.View(state, job));
    }

    public async Task<Result<InvitationDto>> InvitationAsync(Guid learnerId, string invitationId, CancellationToken cancellationToken)
    {
        var invitation = Guid.TryParse(invitationId, out var id) ? await invitations.GetAsync(learnerId, id, cancellationToken) : null;
        if (invitation is null || catalogue.Find(invitation.JobId) is not { } job)
        {
            return CareersErrors.InvitationNotFound;
        }

        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Invitation(invitation, board.View(state, job));
    }

    public async Task<Result<ApplicationDto>> ApplicationAsync(Guid learnerId, string applicationId, CancellationToken cancellationToken)
    {
        var application = Guid.TryParse(applicationId, out var id) ? await applications.GetAsync(learnerId, id, cancellationToken) : null;
        if (application is null || catalogue.Find(application.JobId) is not { } job)
        {
            return CareersErrors.ApplicationNotFound;
        }

        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Application(application, board.View(state, job));
    }

    public async Task<Result<JobDto>> SaveAsync(Guid learnerId, string jobId, bool saved, CancellationToken cancellationToken)
    {
        if (catalogue.Find(jobId) is not { } job)
        {
            return CareersErrors.JobNotFound;
        }

        var existing = await savedJobs.FindAsync(learnerId, jobId, cancellationToken);
        if (saved && existing is null)
        {
            savedJobs.Add(SavedJob.Save(tenant.TenantId, learnerId, jobId, clock.UtcNow));
        }
        else if (!saved && existing is not null)
        {
            savedJobs.Remove(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Dto(board.View(state, job));
    }

    public async Task<Result<ApplyInfoDto>> ApplyInfoAsync(Guid learnerId, string jobId, string displayName, CancellationToken cancellationToken)
    {
        if (catalogue.Find(jobId) is null)
        {
            return CareersErrors.JobNotFound;
        }

        var profile = await directory.CareerProfileAsync(learnerId, cancellationToken);
        var files = await cvFiles.ListAsync(learnerId, cancellationToken);
        return new ApplyInfoDto(
            profile?.Name is { Length: > 0 } name ? name : displayName,
            profile?.Contact ?? string.Empty,
            files.Select(CvOption).ToList(),
            catalogue.QuestionsFor(jobId).Select(q => new ApplyQuestionDto(q.Id, q.Text, q.Kind)).ToList());
    }

    public async Task<Result<CvOptionDto>> UploadCvAsync(Guid learnerId, UploadedFile? file, CancellationToken cancellationToken)
    {
        var read = await CvFileSniffer.ReadAsync(file, Rules.MaxCvBytes, cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        await using var content = read.Value.Content;
        var stored = CvFile.Upload(tenant.TenantId, learnerId, CvFileSniffer.CleanName(file!.FileName, read.Value.ContentType), read.Value.ContentType, content.Length, clock.UtcNow);
        await blobs.UploadAsync(stored.BlobName, content, stored.ContentType, cancellationToken);
        cvFiles.Add(stored);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CvOption(stored);
    }

    public async Task<Result<ApplicationDto>> ApplyAsync(Guid learnerId, string jobId, ApplyRequest request, CancellationToken cancellationToken)
    {
        if (catalogue.Find(jobId) is not { } job)
        {
            return CareersErrors.JobNotFound;
        }

        if (job.Closed)
        {
            return CareersErrors.JobClosed;
        }

        var cv = Guid.TryParse(request.CvId, out var cvId) ? await cvFiles.GetAsync(learnerId, cvId, cancellationToken) : null;
        if (cv is null)
        {
            return CareersErrors.CvNotFound;
        }

        var now = clock.UtcNow;
        if (await applications.LatestForJobAsync(learnerId, jobId, cancellationToken) is { } previous)
        {
            if (previous.Active)
            {
                return CareersErrors.AlreadyApplied;
            }

            if (previous.WithdrawnAt is { } withdrawn && withdrawn.AddDays(Rules.ReapplyCoolOffDays) > now)
            {
                return CareersErrors.ReapplyCoolOff;
            }
        }

        Invitation? invitation = null;
        if (!string.IsNullOrWhiteSpace(request.InvitationId))
        {
            invitation = Guid.TryParse(request.InvitationId, out var invitationId) ? await invitations.GetAsync(learnerId, invitationId, cancellationToken) : null;
            if (invitation is null)
            {
                return CareersErrors.InvitationNotFound;
            }

            if (invitation.IsExpired(clock.Today))
            {
                return CareersErrors.InvitationExpired;
            }

            invitation.Accept(null, now);
        }

        var given = request.Answers ?? new Dictionary<string, string>();
        var answers = catalogue.QuestionsFor(jobId)
            .Where(q => given.TryGetValue(q.Id, out var a) && !string.IsNullOrWhiteSpace(a))
            .Select(q => new ApplicationAnswer(q.Text, given[q.Id].Trim()))
            .ToList();

        var application = JobApplication.Send(tenant.TenantId, learnerId, jobId, cv.Id, cv.Name, answers, invitation?.Id, now);
        applications.Add(application);
        outbox.Add(new ApplicationSent(learnerId, application.Id, jobId) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogApplied(logger, learnerId, jobId);

        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Application(application, board.View(state, job));
    }

    public async Task<Result<InvitationDto>> RespondAsync(Guid learnerId, string invitationId, string action, string? note, CancellationToken cancellationToken)
    {
        var invitation = Guid.TryParse(invitationId, out var id) ? await invitations.GetAsync(learnerId, id, cancellationToken) : null;
        if (invitation is null || catalogue.Find(invitation.JobId) is not { } job)
        {
            return CareersErrors.InvitationNotFound;
        }

        if (invitation.IsExpired(clock.Today))
        {
            return CareersErrors.InvitationExpired;
        }

        var now = clock.UtcNow;
        switch (action)
        {
            case "accept":
                invitation.Accept(note, now);
                break;
            case "decline":
                invitation.Decline(note, now);
                break;
            default:
                invitation.Suggest(note, now);
                break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Invitation(invitation, board.View(state, job));
    }

    public async Task<Result<ApplicationDto>> WithdrawAsync(Guid learnerId, string applicationId, CancellationToken cancellationToken)
    {
        var application = Guid.TryParse(applicationId, out var id) ? await applications.GetAsync(learnerId, id, cancellationToken) : null;
        if (application is null || catalogue.Find(application.JobId) is not { } job)
        {
            return CareersErrors.ApplicationNotFound;
        }

        var wasActive = application.Active;
        if (!application.Withdraw(clock.UtcNow))
        {
            return CareersErrors.CannotWithdraw;
        }

        if (wasActive)
        {
            outbox.Add(new ApplicationWithdrawn(learnerId, application.Id) { TenantId = tenant.TenantId });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        return board.Application(application, board.View(state, job));
    }

    public async Task<AlertSettingsDto> AlertsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: false, cancellationToken);
        return state.Learner is { } l
            ? new AlertSettingsDto(l.JobAlertsOn, l.JobAlertsFrequency, l.JobAlertSearches)
            : new AlertSettingsDto(false, "daily", []);
    }

    public async Task<AlertSettingsDto> SetAlertsAsync(Guid learnerId, AlertSettingsDto alerts, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(learnerId, cancellationToken);
        var searches = alerts.Searches.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        learner.SetJobAlerts(alerts.On, alerts.Frequency, searches, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AlertSettingsDto(learner.JobAlertsOn, learner.JobAlertsFrequency, learner.JobAlertSearches);
    }

    /// <summary>A job's values for each filter key (jobs.handlers.ts <c>jobValues</c>).</summary>
    internal static IReadOnlyList<string>? JobValues(JobView view, string key)
    {
        var j = view.Job;
        return key switch
        {
            "sector" => [j.Sector],
            "role" => [j.Role],
            "state" => j.State.Length > 0 ? [j.State] : [],
            "city" => j.Remote ? [] : [j.City],
            "workMode" => [j.WorkMode],
            "jobType" => [j.JobType],
            "pay" => PayLevels.Where(p => j.PayIndex >= int.Parse(p, CultureInfo.InvariantCulture)).ToList(),
            "posted" => PostedWithin.Where(d => j.PostedDays <= int.Parse(d, CultureInfo.InvariantCulture)).ToList(),
            "match" => MatchLevels.Where(m => view.MatchPercent >= int.Parse(m, CultureInfo.InvariantCulture)).ToList(),
            "jobStatus" => [j.Closed ? "closed" : j.ClosingInDays is not null ? "closing" : "open"],
            _ => null,
        };
    }

    internal static List<JobView> Sort(List<JobView> items, string sort) => sort switch
    {
        "newest" => items.OrderBy(v => v.Job.PostedDays).ToList(),
        "salary" => items.OrderByDescending(v => v.Job.PayIndex).ToList(),
        "match" => items.OrderByDescending(v => v.MatchPercent).ToList(),
        // Most recently saved first.
        "saved" => items.OrderByDescending(v => v.SavedAt ?? DateTime.MaxValue).ToList(),
        "closing" => items.OrderBy(v => v.Job.ClosingInDays ?? 99).ToList(),
        _ => items.OrderByDescending(v => v.MatchPercent).ThenByDescending(v => v.Job.IsNew).ThenBy(v => v.Job.PostedDays).ToList(),
    };

    private List<FacetDto> JobFacets(IReadOnlyList<JobView> items, ListQuery query, string tab)
    {
        var sectors = catalogue.Sectors.Select(s => s.Key).ToList();
        var chosen = query.Filter("sector");
        var roles = (chosen.Count > 0 ? chosen : sectors)
            .SelectMany(s => catalogue.Sectors.FirstOrDefault(x => x.Key == s).Value ?? [])
            .Distinct()
            .ToList();
        var state = query.Filter("state").FirstOrDefault();
        var cities = state is not null
            ? catalogue.States.FirstOrDefault(x => x.Key == state).Value ?? []
            : catalogue.States.SelectMany(x => x.Value).ToList();
        var remotePool = items.Count(v => ListEngine.Passes(v, query.Filters, JobValues, "city") && v.Job.Remote);

        var facets = new List<FacetDto>
        {
            ListEngine.Facet(items, query.Filters, JobValues, "sector", sectors),
            ListEngine.Facet(items, query.Filters, JobValues, "role", roles),
            ListEngine.Facet(items, query.Filters, JobValues, "state", catalogue.States.Select(s => s.Key)),
            ListEngine.Facet(items, query.Filters, JobValues, "city", cities),
            new(ListEngine.RemoteKey, [new FacetOptionDto("yes", "Include remote jobs", remotePool)]),
            ListEngine.Facet(items, query.Filters, JobValues, "workMode", ["onsite", "hybrid", "remote"]),
            ListEngine.Facet(items, query.Filters, JobValues, "jobType", ["full-time", "learnership", "part-time", "contract"]),
            ListEngine.Facet(items, query.Filters, JobValues, "pay", PayLevels),
            ListEngine.Facet(items, query.Filters, JobValues, "posted", PostedWithin),
        };
        if (tab == Matching)
        {
            facets.Insert(0, ListEngine.Facet(items, query.Filters, JobValues, "match", ["90", "80", "70", "60"]));
        }

        if (tab == Bookmarked)
        {
            facets.Insert(0, ListEngine.Facet(items, query.Filters, JobValues, "jobStatus", ["open", "closing", "closed"]));
        }

        return facets;
    }

    private CvOptionDto CvOption(CvFile file)
    {
        var kb = Math.Max(1, (int)Math.Round(file.SizeBytes / 1024.0, MidpointRounding.AwayFromZero));
        var when = clock.DaysSince(file.UploadedAt) <= 0 ? "today" : clock.Short(file.UploadedAt);
        return new CvOptionDto(file.Id.ToString(), file.Name, $"Uploaded {when} · {kb} KB");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Application sent {LearnerId} {JobId}")]
    private static partial void LogApplied(ILogger logger, Guid learnerId, string jobId);
}
