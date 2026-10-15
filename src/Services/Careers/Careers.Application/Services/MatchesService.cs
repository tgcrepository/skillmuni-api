using Careers.Application.Abstractions;
using Careers.Application.Catalogue;
using Careers.Application.Contracts;
using Careers.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Careers.Application.Services;

/// <summary>
/// API-104–109: My Matches (Figma 200:38000, MM1–MM6) and publishing the profile (Q8 Vibe Check "Send it"). Cards
/// come from the market's match catalogue, scored for the learner by <see cref="JobMatcher"/>; they show once the
/// profile is published or an institute claim is confirmed (BR-72).
/// </summary>
public interface IMatchesService
{
    Task<MatchPreviewDto> PreviewAsync(Guid learnerId, CancellationToken cancellationToken);

    Task PublishAsync(Guid learnerId, VisibilitySettings settings, CancellationToken cancellationToken);

    Task<MatchBoardDto> BoardAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result> ApplyAsync(Guid learnerId, string matchId, CancellationToken cancellationToken);

    Task<Result> SaveAsync(Guid learnerId, string matchId, bool saved, CancellationToken cancellationToken);

    Task<Result> AcceptAsync(Guid learnerId, string matchId, CancellationToken cancellationToken);
}

public sealed class MatchesService(
    ICareerCatalogue catalogue,
    JobMatcher matcher,
    CareerStateLoader states,
    IMatchStateRepository matchStates,
    ILearnerDirectory directory,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    CareerClock clock,
    IOptions<CareersOptions> options) : IMatchesService
{
    private const string Recommended = "recommended";

    private CareersOptions Rules => options.Value;

    public async Task<MatchPreviewDto> PreviewAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        var cards = Scored(state, new Dictionary<string, string>());
        return new MatchPreviewDto(
            cards.Count,
            Rules.Matching.RadiusKm,
            cards.Take(2).Select(c => new MatchPreviewTopDto(c.Id, c.Title, c.Kind, c.Match)).ToList());
    }

    public async Task PublishAsync(Guid learnerId, VisibilitySettings settings, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(learnerId, cancellationToken);
        learner.Publish(settings.Discoverable, settings.Invites, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MatchBoardDto> BoardAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var progressTask = directory.ProgressAsync(learnerId, cancellationToken);
        var state = await states.LoadAsync(learnerId, withProfile: true, cancellationToken);
        var progress = (await progressTask).Progress;
        var learner = state.Learner;
        var published = learner?.PublishedAt is not null;
        var visible = published || learner?.ClaimConfirmedAt is not null;
        var cards = visible ? Scored(state, await matchStates.ListAsync(learnerId, cancellationToken)) : [];
        var assessed = learner is { AssessmentDone: true };
        return new MatchBoardDto(
            cards.Count,
            cards.Count(c => c.Status == "shortlisted"),
            cards.Count(c => c.Status == "invited"),
            progress.Percent,
            published && learner!.Discoverable,
            cards,
            assessed ? catalogue.Industries.Select(i => new IndustryFitDto(i.Id, i.Name, i.Fit, i.Why, i.Roles, i.Path, i.PathDone, i.Jobs)).ToList() : [],
            assessed && learner!.AssessmentTakenAt is { } takenAt ? new CompassDto(CareerClock.Iso(clock.LocalDate(takenAt)), learner.ArchetypeSummary ?? string.Empty) : null);
    }

    public Task<Result> ApplyAsync(Guid learnerId, string matchId, CancellationToken cancellationToken) =>
        SetStatusAsync(learnerId, matchId, "applied", cancellationToken);

    public Task<Result> SaveAsync(Guid learnerId, string matchId, bool saved, CancellationToken cancellationToken) =>
        SetStatusAsync(learnerId, matchId, saved ? "saved" : Recommended, cancellationToken);

    public Task<Result> AcceptAsync(Guid learnerId, string matchId, CancellationToken cancellationToken) =>
        SetStatusAsync(learnerId, matchId, "accepted", cancellationToken);

    private async Task<Result> SetStatusAsync(Guid learnerId, string matchId, string status, CancellationToken cancellationToken)
    {
        if (catalogue.MatchesFor(tenant.Market).All(m => m.Id != matchId))
        {
            return CareersErrors.MatchNotFound;
        }

        var existing = await matchStates.FindAsync(learnerId, matchId, cancellationToken);
        if (existing is null)
        {
            matchStates.Add(MatchState.Set(tenant.TenantId, learnerId, matchId, status, clock.UtcNow));
        }
        else
        {
            existing.Change(status, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>The market's cards at the matching threshold or above, best first. Empty while the learner's skills are unknown.</summary>
    private List<MatchJobDto> Scored(CareerState state, IReadOnlyDictionary<string, string> statuses)
    {
        if (state.MatchProfile is not { } profile)
        {
            return [];
        }

        return catalogue.MatchesFor(tenant.Market)
            .Select(seed => (Seed: seed, Result: matcher.Score(seed.Skills, seed.City, seed.State, seed.Remote, profile)))
            .Where(x => x.Result.Percent >= Rules.MatchingThreshold)
            .OrderByDescending(x => x.Result.Percent)
            .Select(x => Card(x.Seed, x.Result, profile, statuses.GetValueOrDefault(x.Seed.Id, Recommended)))
            .ToList();
    }

    private MatchJobDto Card(MatchSeed seed, MatchResult result, MatchProfile profile, string status)
    {
        var declared = result.Matched.FirstOrDefault(m => m.Evidence == "declared");
        var (tip, kind) = result.Missing.Count > 0
            ? ($"Missing: {result.Missing[0]}", "gap")
            : declared is not null
                ? ($"Add {declared.Name} proof → +{matcher.ProofBoost(seed.Skills.Count)}%", "boost")
                : ("Every skill backed by proof", "boost");
        var skills = result.Matched
            .Select(m => new MatchSkillDto(
                profile.Skills.FirstOrDefault(s => JobMatcher.SameSkill(s.Name, m.Name))?.Name ?? m.Name,
                m.Evidence,
                null))
            .ToList();
        return new MatchJobDto(seed.Id, seed.Title, seed.Employer, seed.Kind, seed.Location, null, result.Percent, seed.Pay, status, null, skills, tip, kind, seed.Industry);
    }
}
