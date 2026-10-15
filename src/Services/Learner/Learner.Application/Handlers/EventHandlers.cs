using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Events;
using Learner.Application.Services;
using Learner.Domain;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Privacy;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Handlers;

// Event handlers change tracked entities only; the dispatcher saves them together with the inbox row, so each
// event is applied once. The tenant is set from the event before the handler runs.

/// <summary>Creates the learner (or completes the one a fast first request created) and seeds Quest 1's name.</summary>
public sealed class AccountRegisteredHandler(
    ILearnerRepository learners,
    IQuestAnswersRepository answers,
    TimeProvider clock) : IIntegrationEventHandler<AccountRegistered>
{
    private const string WhoDis = "who-dis";

    public async Task HandleAsync(AccountRegistered e, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var learner = await learners.GetAsync(e.AccountId, cancellationToken);
        if (learner is null)
        {
            learner = LearnerProfile.Create(e.AccountId, e.TenantId, e.Market, e.FirstName, e.LastName, now);
            learners.Add(learner);
        }
        else if (learner.LastName.Length == 0)
        {
            learner.Rename(e.FirstName, e.LastName, now);
        }

        learner.SetVerifiedContact(e.Phone, e.Email, now);

        if (await answers.FindAsync(e.AccountId, WhoDis, cancellationToken) is null)
        {
            var seeded = QuestAnswers.Start(e.TenantId, e.AccountId, WhoDis, now);
            seeded.Replace(JsonSerializer.Serialize(new { firstName = e.FirstName, lastName = e.LastName }), now);
            answers.Add(seeded);
        }
    }
}

/// <summary>A-273: keeps the verified mobile / email copy the Profile and Quest 2 show.</summary>
public sealed class ContactVerifiedHandler(ILearnerRepository learners, TimeProvider clock, ILogger<ContactVerifiedHandler> logger)
    : IIntegrationEventHandler<ContactVerified>
{
    public async Task HandleAsync(ContactVerified e, CancellationToken cancellationToken)
    {
        var learner = await learners.GetAsync(e.AccountId, cancellationToken);
        if (learner is null)
        {
            // The registration event carries the contacts too, so nothing is lost.
            logger.LogInformation("Contact verified before the learner exists {LearnerId}", e.AccountId);
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (e.Kind == "phone")
        {
            learner.SetVerifiedContact(e.Value, null, now);
        }
        else if (e.Kind == "email")
        {
            learner.SetVerifiedContact(null, e.Value, now);
        }
    }
}

/// <summary>
/// Claim V4 (BR-119): the institute's CV pre-filled quests 1–5, so they count as done, and the learner earns the
/// normal step and quest XP for them (550) through the usual events: every step of each newly done quest, then the
/// quest itself (its badge follows in Gamification). Steps already paid are deduplicated by Gamification's ledger.
/// </summary>
/// <remarks>
/// <c>identity.learner.registered</c> (published at the claim's code step) can be delivered after this event. The
/// learner is then started here and the registration event fills in the name and contacts when it lands
/// (<see cref="AccountRegisteredHandler"/>), so the claimed quests and their 550 XP are never dropped.
/// </remarks>
public sealed partial class ClaimConfirmedHandler(
    ILearnerRepository learners,
    QuestCatalog catalog,
    IJobProfileCompletion jobProfile,
    IOutbox outbox,
    ITenantContext tenant,
    TimeProvider clock,
    ILogger<ClaimConfirmedHandler> logger) : IIntegrationEventHandler<ClaimConfirmed>
{
    public const int PrefilledQuests = 5;

    public async Task HandleAsync(ClaimConfirmed e, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (await learners.GetAsync(e.AccountId, cancellationToken) is not { } learner)
        {
            learner = LearnerProfile.Create(e.AccountId, e.TenantId, tenant.Market, string.Empty, string.Empty, now);
            learners.Add(learner);
            LogNoLearner(logger, e.AccountId);
        }

        var done = learner.FastForwardQuests(PrefilledQuests, now);
        foreach (var quest in catalog.Quests.Where(q => done.Contains(q.Number)))
        {
            for (var part = 1; part <= quest.Parts.Count; part++)
            {
                outbox.Add(new QuestStepCompleted(learner.Id, quest.Slug, quest.Number, part, Paid: true) { TenantId = e.TenantId });
            }

            outbox.Add(new QuestCompleted(learner.Id, quest.Slug, quest.Number, quest.Badge) { TenantId = e.TenantId });
        }

        await jobProfile.CheckAsync(learner, null, null, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Claim confirmed before the registration event: learner started {LearnerId}")]
    private static partial void LogNoLearner(ILogger logger, Guid learnerId);
}

/// <summary>
/// MISS-08: files first (so a retry can still find them), then every row; a tombstone (id + time) stays so the
/// learner's still-valid token gets 410 and late events are skipped instead of recreating an empty profile.
/// </summary>
public sealed class AccountErasedHandler(
    ILearnerDataEraser eraser,
    IBlobStore blobs,
    IErasureTombstones tombstones,
    ILogger<AccountErasedHandler> logger)
    : IIntegrationEventHandler<AccountErased>
{
    public async Task HandleAsync(AccountErased e, CancellationToken cancellationToken)
    {
        var names = await eraser.ListBlobsAsync(e.AccountId, cancellationToken);
        foreach (var name in names)
        {
            await blobs.DeleteAsync(name, cancellationToken);
        }

        await eraser.EraseAsync(e.AccountId, cancellationToken);
        await tombstones.MarkErasedAsync(e.AccountId, cancellationToken);
        logger.LogInformation("Learner data erased {LearnerId} {Files}", e.AccountId, names.Count);
    }
}

/// <summary>Gamification's totals: the projection Profile and the quest HUD read.</summary>
public sealed class XpAwardedHandler(ILearnerRepository learners, TimeProvider clock) : IIntegrationEventHandler<XpAwarded>
{
    public async Task HandleAsync(XpAwarded e, CancellationToken cancellationToken)
    {
        if (await learners.GetAsync(e.LearnerId, cancellationToken) is { } learner)
        {
            learner.ApplyProgress(e.TotalXp, e.Level, e.Rank, e.LevelStartXp, e.NextLevelXp, e.Badges, e.StreakDays, clock.GetUtcNow().UtcDateTime);
        }
    }
}

/// <summary>A-306: the archetype on the Player ID comes from the learner's own Career Drivers result.</summary>
public sealed class AssessmentSubmittedHandler(ILearnerRepository learners, TimeProvider clock) : IIntegrationEventHandler<AssessmentSubmitted>
{
    public async Task HandleAsync(AssessmentSubmitted e, CancellationToken cancellationToken)
    {
        if (await learners.GetAsync(e.LearnerId, cancellationToken) is { } learner)
        {
            learner.ApplyArchetype(e.ArchetypeName, e.ArchetypeSummary, clock.GetUtcNow().UtcDateTime);
        }
    }
}

/// <summary>
/// A-245: answers read from a CV pre-fill the quests; anything the learner already saved is kept. The read is also
/// kept for the Job Profile's "Upload a newer CV" suggestions (P5).
/// </summary>
public sealed class CvParsedHandler(
    IQuestAnswersRepository answers,
    IJobProfileRepository jobProfiles,
    QuestCatalog catalog,
    TimeProvider clock) : IIntegrationEventHandler<CvParsed>
{
    public async Task HandleAsync(CvParsed e, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // Two parse results close together must not both insert the row (duplicate key → redelivery, error log).
        var settings = await jobProfiles.GetOrCreateAsync(JobProfileSettings.Create(e.TenantId, e.LearnerId, ShareCodes.New(), now), cancellationToken);
        settings.KeepCvRead(e.CvId, JsonSerializer.Serialize(e.Prefill), now);

        foreach (var (slug, values) in e.Prefill)
        {
            if (catalog.Find(slug) is null)
            {
                continue;
            }

            // Insert-if-absent: the learner may be saving this quest (or another parse result arriving) right now.
            var record = await answers.FindAsync(e.LearnerId, slug, cancellationToken)
                ?? await answers.GetOrCreateAsync(QuestAnswers.Start(e.TenantId, e.LearnerId, slug, now), cancellationToken);
            record.Replace(AnswersJson.Seed(record.AnswersJson, values), now);
        }
    }
}
