using Careers.Application.Abstractions;
using Careers.Application.Catalogue;
using Careers.Application.Contracts;
using Careers.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;

namespace Careers.Application.Services;

/// <summary>
/// API-090–096: Career Launchpad v2 (Figma 241:18014). One endpoint per card so one slow source never blocks the
/// page. Missions tick from real activity (BR-60, A-306): the assessment result and the Job Profile's 100 % come from
/// events, applications and bookings from this service.
/// </summary>
public interface ILaunchpadService
{
    Task<ReadinessDto> ReadinessAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result<ProfileCardDto>> ProfileAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<AssessmentCardDto> AssessmentAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<JobsCardDto> JobsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<FestCardDto> FestAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorDto>> SectorsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<FestCardDto> SetFestAlertsAsync(Guid learnerId, bool on, CancellationToken cancellationToken);
}

public sealed class LaunchpadService(
    CareerStateLoader states,
    JobBoard board,
    FestViews festViews,
    IFestCatalogue fests,
    ICareerCatalogue career,
    IFestBookingRepository bookings,
    IInvitationRepository invitations,
    ILearnerDirectory directory,
    IUnitOfWork unitOfWork,
    CareerClock clock,
    IOptions<CareersOptions> options) : ILaunchpadService
{
    /// <summary>Placeholder emoji per archetype (assessment.data.ts) until Assessment sends one with the result.</summary>
    private static readonly Dictionary<string, string> ArchetypeEmoji = new(StringComparer.OrdinalIgnoreCase)
    {
        ["The People-First Service Builder"] = "🤝",
        ["The Commercial Rainmaker"] = "💼",
        ["The Specialist Operator"] = "🔧",
        ["The Creative Pathfinder"] = "🌈",
        ["The Purpose-Driven Community Leader"] = "🌱",
        ["The Strategic Leader"] = "👑",
    };

    private CareersOptions Rules => options.Value;

    public async Task<ReadinessDto> ReadinessAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: false, cancellationToken);
        var profileDone = state.Learner?.JobProfileCompletedAt is not null
            || (await directory.ProgressAsync(learnerId, cancellationToken)).Progress.Percent >= 100;
        var booked = (await bookings.ListAsync(learnerId, cancellationToken)).Count > 0;
        var m = Rules.Missions;
        var missions = new List<MissionDto>
        {
            new("assessment", "Take assessment", m.AssessmentXp, state.Learner?.AssessmentDone == true),
            new("profile", "Finish job profile", m.ProfileXp, profileDone),
            new("apply", "Apply to a job", m.ApplyXp, state.ActiveApplications > 0),
            new("fest", "Book a Hire Fest slot", m.FestXp, booked),
        };

        // BR-61: level = missions done; readiness % = XP of done missions ÷ 350.
        var done = missions.Count(x => x.Done);
        var total = missions.Sum(x => x.Xp);
        var percent = total == 0 ? 0 : (int)Math.Round(missions.Where(x => x.Done).Sum(x => x.Xp) * 100.0 / total, MidpointRounding.AwayFromZero);
        var names = m.Levels();
        return new ReadinessDto(done, names[Math.Min(done, names.Count - 1)], percent, missions);
    }

    public async Task<Result<ProfileCardDto>> ProfileAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var lookup = await directory.ProgressAsync(learnerId, cancellationToken);
        if (lookup.Status == LookupStatus.Unavailable)
        {
            return CareersErrors.LearnerUnavailable;
        }

        var p = lookup.Progress;
        var missing = p.WithGaps.Select(q => q.Name).ToList();
        return new ProfileCardDto(p.Percent, missing.Count, p.Quests.Count, p.Total - p.Filled, missing);
    }

    public async Task<AssessmentCardDto> AssessmentAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: false, cancellationToken);
        var a = Rules.Assessment;

        // Only the submitted result reaches Careers (assessment.attempt.submitted), so "paused" isn't known here.
        if (state.Learner is not { AssessmentDone: true } learner)
        {
            return new AssessmentCardDto("not-started", 0, a.Questions, a.Minutes, null, 0);
        }

        var name = learner.ArchetypeName ?? string.Empty;
        var archetype = new ArchetypeDto(name, ArchetypeEmoji.GetValueOrDefault(name, "✨"), learner.TopDrivers.Take(3).ToList());
        return new AssessmentCardDto("done", a.Questions, a.Questions, 0, archetype, a.MandatoryCourses);
    }

    public async Task<JobsCardDto> JobsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        var matches = board.Views(state)
            .Where(v => v.MatchPercent >= board.Threshold)
            .OrderByDescending(v => v.MatchPercent).ThenByDescending(v => v.Job.IsNew).ThenBy(v => v.Job.PostedDays)
            .ToList();
        var now = clock.UtcNow;
        var interviews = (await invitations.ListAsync(learnerId, cancellationToken))
            .Count(i => i.Type == "interview" && i.Status == InvitationStatus.Accepted && i.InterviewAt > now)
            + state.Applications.Count(a => a.Status == ApplicationStatus.Interview);
        var top = matches.FirstOrDefault() is { } best ? new TopJobDto(best.Job.Title, best.Job.Company, best.Job.City, best.MatchPercent) : null;

        // A-261: My Jobs is never locked. BR-62: visiting My Jobs clears "new matches".
        return new JobsCardDto(
            false,
            0,
            matches.Count,
            state.ActiveApplications,
            state.Saved.Count,
            state.Learner?.JobsSeenAt is null ? matches.Count : 0,
            interviews,
            top);
    }

    public async Task<FestCardDto> FestAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: false, cancellationToken);
        return await FestCardAsync(learnerId, state.Learner, cancellationToken);
    }

    public async Task<IReadOnlyList<SectorDto>> SectorsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: false, cancellationToken);

        // BR-63: fit is blank until the assessment is done; then sectors sort by fit (FR-37).
        var assessed = state.Learner?.AssessmentDone == true;
        var sectors = career.Sectors.Select(s => new SectorDto(s.Id, s.Name, s.Emoji, s.Jobs, s.NewJobs, assessed ? s.Fit : null));
        return (assessed ? sectors.OrderByDescending(s => s.Fit) : sectors).ToList();
    }

    public async Task<FestCardDto> SetFestAlertsAsync(Guid learnerId, bool on, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(learnerId, cancellationToken);
        learner.SetFestAlerts(on, on, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await FestCardAsync(learnerId, learner, cancellationToken);
    }

    /// <summary>The learner's next booked fest, else the next fest that is still open for booking.</summary>
    private async Task<FestCardDto> FestCardAsync(Guid learnerId, CareerLearner? learner, CancellationToken cancellationToken)
    {
        var alertsOn = learner is not null && (learner.FestAlertsCity || learner.FestAlertsOnline);
        var mine = (await bookings.ListAsync(learnerId, cancellationToken))
            .Select(b => (Booking: b, Fest: fests.Find(b.FestId)))
            .Where(x => x.Fest is not null && !festViews.IsEnded(x.Fest))
            .OrderBy(x => x.Fest!.Start)
            .FirstOrDefault();
        var fest = mine.Fest ?? fests.Fests.Where(festViews.IsBookable).OrderBy(f => f.Start).FirstOrDefault();
        if (fest is null)
        {
            return new FestCardDto(null, alertsOn);
        }

        var usage = await bookings.UsageAsync([fest.Id], cancellationToken);
        var first = mine.Booking?.Picks.OrderBy(p => p.Date).ThenBy(p => p.Time, StringComparer.Ordinal).FirstOrDefault();
        return new FestCardDto(
            new FestCardFestDto(
                fest.Id,
                fest.Name,
                CareerClock.Iso(fest.Start),
                Math.Max(0, clock.DaysUntil(fest.Start)),
                fest.EmployerCount,
                fest.Free,
                fest.Online ? "Online" : "In person",
                festViews.SeatsLeft(fest, usage),
                fest.SeatsTotal,
                first is null ? null : FestViews.Clock(first.Time).ToUpperInvariant()),
            alertsOn);
    }
}
