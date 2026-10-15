using System.Globalization;
using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Errors;
using Learner.Application.Events;
using Learner.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-126/127/128: the 8 registration quests (answers survive save &amp; exit, A-252).</summary>
public interface IQuestService
{
    Task<Result<JsonElement>> GetAnswersAsync(LearnerRef who, string slug, CancellationToken cancellationToken);

    /// <summary>Idempotent per part: saving the same part again changes nothing and earns nothing.</summary>
    Task<Result> SavePartAsync(LearnerRef who, string slug, int part, SavePartRequest request, CancellationToken cancellationToken);

    Task<Result<QuestCompletionDto>> CompleteAsync(LearnerRef who, string slug, CancellationToken cancellationToken);
}

public sealed partial class QuestService(
    ILearnerAccessor accessor,
    IQuestAnswersRepository answers,
    IProgressService progress,
    QuestCatalog catalog,
    IJobProfileCompletion jobProfile,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options,
    ILogger<QuestService> logger) : IQuestService
{
    private const string WhoDis = "who-dis";

    public async Task<Result<JsonElement>> GetAnswersAsync(LearnerRef who, string slug, CancellationToken cancellationToken)
    {
        if (catalog.Find(slug) is null)
        {
            return LearnerErrors.QuestNotFound;
        }

        var saved = await answers.FindAsync(who.Id, slug, cancellationToken);
        if (saved is null && slug == WhoDis)
        {
            // Quest 1 starts with the registered name (A-232), even before the sign-up event has seeded the answers.
            var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
            return AnswersJson.ToElement(JsonSerializer.Serialize(new { firstName = learner.FirstName, lastName = learner.LastName }));
        }

        return AnswersJson.ToElement(saved?.AnswersJson ?? AnswersJson.Empty);
    }

    public async Task<Result> SavePartAsync(LearnerRef who, string slug, int part, SavePartRequest request, CancellationToken cancellationToken)
    {
        var quest = catalog.Find(slug);
        if (quest is null)
        {
            return LearnerErrors.QuestNotFound;
        }

        if (part < 1 || part > quest.Parts.Count)
        {
            return LearnerErrors.PartNotFound;
        }

        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;

        var existing = await answers.FindAsync(who.Id, slug, cancellationToken);
        var merged = AnswersJson.Merge(existing?.AnswersJson ?? AnswersJson.Empty, request.Values);
        if (AnswersJson.Utf8Length(merged) > options.Value.MaxQuestAnswersBytes)
        {
            return LearnerErrors.AnswersTooLarge;
        }

        // First save of this quest: insert-if-absent, so a CV pre-fill (cv.parse.completed) creating the same row at
        // the same moment can't hit the unique key; if it won, merge onto what it stored.
        var record = existing ?? await answers.GetOrCreateAsync(QuestAnswers.Start(tenant.TenantId, who.Id, slug, now), cancellationToken);
        if (existing is null)
        {
            merged = AnswersJson.Merge(record.AnswersJson, request.Values);
        }

        record.Replace(merged, now);

        // Quest 1's name step is the learner's name everywhere (Profile, Arena, CV).
        if (slug == WhoDis && AnswersJson.ReadString(request.Values, "firstName") is { Length: > 0 } first)
        {
            learner.Rename(first, AnswersJson.ReadString(request.Values, "lastName") ?? learner.LastName, now);
        }

        // Quest 1's birthday ("DD / MM / YYYY", birthday.ts) and gender (q1-tea.ts) pre-fill the Profile once.
        if (slug == WhoDis)
        {
            var gender = AnswersJson.ReadString(request.Values, "gender") is { Length: > 0 } key and not "self-describe" ? ProfileFormat.ParseGender(key) : (Gender?)null;
            var birthday = DateOnly.TryParseExact(AnswersJson.ReadString(request.Values, "birthday") ?? string.Empty, "dd / MM / yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date <= DateOnly.FromDateTime(now)
                ? date
                : (DateOnly?)null;
            learner.SeedFromQuest(gender, birthday, now);
        }

        // BR-100: +20 the first time a step of the quest in progress is done. BR-101: an all-optional step pays only
        // when something was added; "Skip for now" (xp: false, <key>Skipped) or an empty Next pays 0 and, since the
        // step is then done, can never pay later. Gamification counts the step either way. A-168: bonusXp is ignored.
        if (request.Done && learner.RecordPartDone(quest.Number, part, now))
        {
            var step = quest.Parts[part - 1];
            var paid = !step.Optional || (request.Xp != false && !AnswersJson.IsSkipped(merged, step.Key) && AnswersJson.HasContent(request.Values));
            outbox.Add(new QuestStepCompleted(who.Id, slug, quest.Number, part, paid) { TenantId = tenant.TenantId });
            if (paid)
            {
                learner.RecordAwardedXp(options.Value.Xp.QuestStep, now);
            }

            LogStep(logger, who.Id, quest.Number, part, paid);
        }

        await jobProfile.CheckAsync(learner, slug, merged, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<QuestCompletionDto>> CompleteAsync(LearnerRef who, string slug, CancellationToken cancellationToken)
    {
        var quest = catalog.Find(slug);
        if (quest is null)
        {
            return LearnerErrors.QuestNotFound;
        }

        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        if (quest.Number > learner.QuestsCompleted + 1)
        {
            return LearnerErrors.QuestLocked;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var earned = 0;
        if (learner.CompleteQuest(quest.Number, now))
        {
            earned = options.Value.Xp.QuestComplete;
            outbox.Add(new QuestCompleted(who.Id, slug, quest.Number, quest.Badge) { TenantId = tenant.TenantId });
            learner.RecordAwardedXp(earned, now);
            await jobProfile.CheckAsync(learner, null, null, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogCompleted(logger, who.Id, quest.Number);
        }

        return new QuestCompletionDto(earned, progress.For(learner));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Quest step done {LearnerId} {Quest} {Part} {Paid}")]
    private static partial void LogStep(ILogger logger, Guid learnerId, int quest, int part, bool paid);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quest completed {LearnerId} {Quest}")]
    private static partial void LogCompleted(ILogger logger, Guid learnerId, int quest);
}
