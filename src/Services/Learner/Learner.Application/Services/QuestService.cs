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

        var record = await answers.FindAsync(who.Id, slug, cancellationToken);
        if (record is null)
        {
            record = QuestAnswers.Start(tenant.TenantId, who.Id, slug, now);
            answers.Add(record);
        }

        var merged = AnswersJson.Merge(record.AnswersJson, request.Values);
        if (AnswersJson.Utf8Length(merged) > options.Value.MaxQuestAnswersBytes)
        {
            return LearnerErrors.AnswersTooLarge;
        }

        record.Replace(merged, now);

        // Quest 1's name step is the learner's name everywhere (Profile, Arena, CV).
        if (slug == WhoDis && AnswersJson.ReadString(request.Values, "firstName") is { Length: > 0 } first)
        {
            learner.Rename(first, AnswersJson.ReadString(request.Values, "lastName") ?? learner.LastName, now);
        }

        // A-249: +20 the first time a step of the quest in progress is done; skipped steps send xp: false.
        // A-168: bonusXp (extras) earns nothing and is ignored.
        if (request.Done && learner.RecordPartDone(quest.Number, part, now) && request.Xp != false)
        {
            outbox.Add(new QuestStepCompleted(who.Id, slug, quest.Number, part) { TenantId = tenant.TenantId });
            learner.RecordAwardedXp(options.Value.Xp.QuestStep, now);
            LogStep(logger, who.Id, quest.Number, part);
        }

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
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogCompleted(logger, who.Id, quest.Number);
        }

        return new QuestCompletionDto(earned, progress.For(learner));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Quest step done {LearnerId} {Quest} {Part}")]
    private static partial void LogStep(ILogger logger, Guid learnerId, int quest, int part);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quest completed {LearnerId} {Quest}")]
    private static partial void LogCompleted(ILogger logger, Guid learnerId, int quest);
}
