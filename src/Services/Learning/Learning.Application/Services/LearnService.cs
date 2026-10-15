using System.Text.Json;
using Learning.Application.Contracts;
using Learning.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Learning.Application.Services;

/// <summary>API-097…103 and MISS-04: modules, topics, bookmarks, feedback, quizzes and learning minutes.</summary>
public interface ILearnService
{
    Task<Result<ModuleDetailDto>> ModuleAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken);

    Task<Result<TopicDetailDto>> TopicAsync(Guid learnerId, string moduleSlug, string topicSlug, CancellationToken cancellationToken);

    Task<Result> BookmarkAsync(Guid learnerId, string briefId, bool on, CancellationToken cancellationToken);

    Task<Result> FeedbackAsync(Guid learnerId, string briefId, bool? helpful, CancellationToken cancellationToken);

    Task<Result<QuizDto>> QuizAsync(Guid learnerId, string moduleSlug, string topicSlug, CancellationToken cancellationToken);

    Task<Result> SaveDraftAsync(Guid learnerId, string quizId, IReadOnlyDictionary<string, string> answers, CancellationToken cancellationToken);

    Task<Result<QuizResultDto>> SubmitAsync(Guid learnerId, string quizId, IReadOnlyDictionary<string, string> answers, CancellationToken cancellationToken);

    Task<Result> RecordActivityAsync(Guid learnerId, int minutes, CancellationToken cancellationToken);
}

public sealed partial class LearnService(
    ILearningContent content,
    IBriefProgressRepository progress,
    IQuizRepository quizzes,
    IActivityRepository activity,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearningOptions> options,
    ILogger<LearnService> logger) : ILearnService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ModuleDetailDto>> ModuleAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken)
    {
        if (content.Module(moduleSlug) is not { } module)
        {
            return LearningErrors.ModuleNotFound;
        }

        var state = ModuleProgress.From(module, await progress.ListModuleAsync(learnerId, module.Slug, cancellationToken));
        return new ModuleDetailDto(
            module.Slug,
            module.Title,
            module.Tagline,
            new SlugTitleDto(module.Knook.Slug, module.Knook.Title),
            state.QuizzesDone,
            state.QuizzesTotal,
            state.Xp,
            module.Topics.Select(state.Summary).ToList(),
            state.ResumeDto());
    }

    public async Task<Result<TopicDetailDto>> TopicAsync(Guid learnerId, string moduleSlug, string topicSlug, CancellationToken cancellationToken)
    {
        if (Find(moduleSlug, topicSlug) is not { } found)
        {
            return LearningErrors.TopicNotFound;
        }

        var (module, topic) = found;

        var rows = await progress.ListModuleAsync(learnerId, module.Slug, cancellationToken);
        var state = ModuleProgress.From(module, rows);
        var byIndex = rows.Where(r => r.TopicSlug == topic.Slug).GroupBy(r => r.BriefIndex).ToDictionary(g => g.Key, g => g.First());

        var briefs = topic.Briefs.Select((brief, i) =>
        {
            var row = byIndex.GetValueOrDefault(i);
            var id = new BriefKey(module.Slug, topic.Slug, i).ToString();
            var result = row is { LastCorrect: { } correct, LastTotal: { } total } ? new QuizScoreDto(correct, total) : null;
            return new BriefDto(
                id,
                i + 1,
                brief.Title,
                state.StatusOf(topic, i),
                brief.Body,
                brief.Example,
                brief.KeyTerm,
                brief.VideoUrl,
                brief.Media is { } m ? new BriefMediaDto(m.Kind, m.Src, m.Alt, m.Poster, m.Duration) : null,
                row?.Bookmarked ?? false,
                row?.Helpful,
                state.XpOf(topic, i),
                new BriefQuizDto(id, topic.QuestionsFor(i).Count, result));
        }).ToList();

        var course = topic.Course is { } title
            ? new CourseCardDto(title, state.IsDone(topic), Links.Course(module.Slug, topic.Slug))
            : null;
        return new TopicDetailDto(
            topic.Slug,
            topic.Title,
            new SlugTitleDto(module.Slug, module.Title),
            new TitleDto(module.Knook.Title),
            state.Done(topic),
            topic.Briefs.Count,
            state.TopicXp(topic),
            briefs,
            course);
    }

    public Task<Result> BookmarkAsync(Guid learnerId, string briefId, bool on, CancellationToken cancellationToken) =>
        UpdateBriefAsync(learnerId, briefId, (row, now) => row.Bookmark(on, now), cancellationToken);

    public Task<Result> FeedbackAsync(Guid learnerId, string briefId, bool? helpful, CancellationToken cancellationToken) =>
        UpdateBriefAsync(learnerId, briefId, (row, now) => row.RateHelpful(helpful, now), cancellationToken);

    public async Task<Result<QuizDto>> QuizAsync(Guid learnerId, string moduleSlug, string topicSlug, CancellationToken cancellationToken)
    {
        if (Find(moduleSlug, topicSlug) is not { } found)
        {
            return LearningErrors.TopicNotFound;
        }

        var (module, topic) = found;

        var state = ModuleProgress.From(module, await progress.ListModuleAsync(learnerId, module.Slug, cancellationToken));
        var brief = state.QuizBrief(topic);
        var id = new BriefKey(module.Slug, topic.Slug, brief).ToString();
        var draft = await quizzes.GetDraftAsync(learnerId, id, cancellationToken);

        return new QuizDto(
            id,
            topic.QuizTitle ?? topic.Briefs[brief].Title,
            new SlugTitleDto(module.Slug, module.Title),
            new SlugTitleDto(topic.Slug, topic.Title),
            new TitleDto(module.Knook.Title),
            state.Done(topic),
            topic.Briefs.Count,
            state.TopicXp(topic),
            topic.QuestionsFor(brief)
                .Select(q => new QuizQuestionDto(q.Id, q.Prompt, q.Options.Select((text, i) => new QuizOptionDto(q.OptionId(i), text)).ToList()))
                .ToList(),
            ReadDraft(draft),
            state.IsPassed(topic, brief));
    }

    public async Task<Result> SaveDraftAsync(Guid learnerId, string quizId, IReadOnlyDictionary<string, string> answers, CancellationToken cancellationToken)
    {
        if (FindBrief(quizId) is null)
        {
            return LearningErrors.QuizNotFound;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var draft = await quizzes.GetDraftAsync(learnerId, quizId, cancellationToken);
        if (draft is null)
        {
            draft = QuizDraft.Start(tenant.TenantId, learnerId, quizId, now);
            quizzes.Add(draft);
        }

        draft.Save(JsonSerializer.Serialize(answers, Json), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<QuizResultDto>> SubmitAsync(Guid learnerId, string quizId, IReadOnlyDictionary<string, string> answers, CancellationToken cancellationToken)
    {
        if (FindBrief(quizId) is not { } found)
        {
            return LearningErrors.QuizNotFound;
        }

        var (module, topic, key) = found;

        var rows = await progress.ListModuleForUpdateAsync(learnerId, module.Slug, cancellationToken);
        var before = ModuleProgress.From(module, rows);
        var first = !before.IsPassed(topic, key.Index);
        if (first && key.Index != before.Current(topic))
        {
            return LearningErrors.QuizLocked;
        }

        // Grade: the picked option id must equal the right one (the mock's rule).
        var questions = topic.QuestionsFor(key.Index);
        var results = questions.Select(q =>
        {
            var picked = answers.GetValueOrDefault(q.Id) ?? string.Empty;
            var correct = picked == q.CorrectOptionId;
            var note = correct ? null : q.Notes?.GetValueOrDefault(q.IndexOf(picked));
            return new QuestionResultDto(q.Id, q.CorrectOptionId, picked, correct, q.Why, note);
        }).ToList();
        var right = results.Count(r => r.Correct);

        var now = clock.GetUtcNow().UtcDateTime;
        var id = key.ToString();
        var row = rows.FirstOrDefault(r => r.BriefId == id);
        if (row is null)
        {
            row = BriefProgress.Start(tenant.TenantId, learnerId, key, now);
            progress.Add(row);
        }

        var xp = row.RecordQuiz(right, questions.Count, now);
        quizzes.Add(QuizAttempt.Record(tenant.TenantId, learnerId, id, right, questions.Count, xp, first, now));
        if (await quizzes.GetDraftAsync(learnerId, id, cancellationToken) is { } draft)
        {
            quizzes.Remove(draft);
        }

        outbox.Add(new QuizSubmitted(learnerId, id, id, topic.Slug, module.Slug, right, questions.Count, first) { TenantId = tenant.TenantId });
        if (first)
        {
            PublishCompletions(learnerId, module, topic, id, rows.Where(r => r.Passed).Select(ModuleProgress.ToPassed).Append(ModuleProgress.ToPassed(row)));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogQuizSubmitted(logger, learnerId, id, first, right, questions.Count);
        return new QuizResultDto(right, questions.Count, xp, results, MuniLine(questions, results));
    }

    public async Task<Result> RecordActivityAsync(Guid learnerId, int minutes, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, options.Value.TimeZoneFor(tenant.Market)).DateTime);
        var row = await activity.GetAsync(learnerId, day, cancellationToken);
        if (row is null)
        {
            row = ActivityDay.On(tenant.TenantId, learnerId, day);
            activity.Add(row);
        }

        var added = row.Add(minutes, options.Value.MaxDailyMinutes);
        if (added > 0)
        {
            outbox.Add(new ActivityRecorded(learnerId, added) { TenantId = tenant.TenantId });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Muni's speech bubble on the results screen (the mock's copy).</summary>
    internal static string MuniLine(IReadOnlyList<QuizQuestion> questions, IReadOnlyList<QuestionResultDto> results)
    {
        var right = results.Count(r => r.Correct);
        if (right == questions.Count)
        {
            return "Brain on fire today!";
        }

        if (right == 0)
        {
            return "Shake it off. Read the why, then go again.";
        }

        var missed = questions.Where((_, i) => !results[i].Correct).FirstOrDefault();
        return missed?.Muni ?? "So close! Check the one you missed.";
    }

    private void PublishCompletions(Guid learnerId, ModuleContent module, TopicContent topic, string briefId, IEnumerable<PassedBrief> passedNow)
    {
        outbox.Add(new BriefCompleted(learnerId, briefId, topic.Slug, module.Slug, FirstCompletion: true) { TenantId = tenant.TenantId });

        var after = new ModuleProgress(module, passedNow);
        if (after.IsDone(topic))
        {
            outbox.Add(new ModuleCompleted(learnerId, topic.Slug, module.Slug) { TenantId = tenant.TenantId });
        }

        if (after.Completed)
        {
            outbox.Add(new CourseCompleted(learnerId, module.Slug) { TenantId = tenant.TenantId });
        }
    }

    private async Task<Result> UpdateBriefAsync(Guid learnerId, string briefId, Action<BriefProgress, DateTime> change, CancellationToken cancellationToken)
    {
        if (FindBrief(briefId) is not { } found)
        {
            return LearningErrors.BriefNotFound;
        }

        var key = found.Key;

        var now = clock.GetUtcNow().UtcDateTime;
        var row = await progress.GetAsync(learnerId, key.ToString(), cancellationToken);
        if (row is null)
        {
            row = BriefProgress.Start(tenant.TenantId, learnerId, key, now);
            progress.Add(row);
        }

        change(row, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private (ModuleContent Module, TopicContent Topic)? Find(string moduleSlug, string topicSlug) =>
        content.Module(moduleSlug) is { } module && module.Topic(topicSlug) is { } topic ? (module, topic) : null;

    /// <summary>The brief (and quiz) behind an id, when the catalogue has it.</summary>
    private (ModuleContent Module, TopicContent Topic, BriefKey Key)? FindBrief(string id) =>
        BriefKey.TryParse(id, out var key) && Find(key.Module, key.Topic) is { } found && key.Index < found.Topic.Briefs.Count
            ? (found.Module, found.Topic, key)
            : null;

    private static Dictionary<string, string> ReadDraft(QuizDraft? draft)
    {
        if (draft is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(draft.AnswersJson, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Quiz submitted {LearnerId} {QuizId} first {FirstAttempt} {Correct}/{Total}")]
    private static partial void LogQuizSubmitted(ILogger logger, Guid learnerId, string quizId, bool firstAttempt, int correct, int total);
}
