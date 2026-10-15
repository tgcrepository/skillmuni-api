using Learning.Application;
using Learning.Application.Contracts;
using Learning.Application.Handlers;

namespace Learning.UnitTests;

public sealed class LearnServiceTests
{
    private const string BrainVita = "brain-vita";
    private const string NonVerbal = "non-verbal-reasoning";
    private const string Data = "data-interpretation";

    private readonly Harness _h = new();

    [Fact]
    public async Task A_new_learner_sees_the_module_untouched_with_the_first_brief_as_resume_point()
    {
        var module = (await _h.Learn.ModuleAsync(_h.LearnerId, BrainVita, default)).Value;

        Assert.Equal("Brain Vita", module.Title);
        Assert.Equal(new SlugTitleDto("skill", "Skill Knook"), module.Knook);
        Assert.Equal(new[] { "verbal-reasoning", NonVerbal, "arithmetical-ability", Data }, module.Topics.Select(t => t.Slug));
        Assert.Equal(51 + 11 + 37 + 6, module.QuizzesTotal);
        Assert.Equal(0, module.QuizzesDone);
        Assert.Equal(0, module.Xp);
        Assert.Equal("brain-vita~verbal-reasoning~0", module.Resume!.Brief.Id);
        Assert.Equal(1, module.Resume.Brief.Index);
        Assert.Equal("/learn/brain-vita/verbal-reasoning", module.Resume.Link);
    }

    [Fact]
    public async Task Unknown_module_or_topic_is_404()
    {
        Assert.Equal("module-not-found", (await _h.Learn.ModuleAsync(_h.LearnerId, "nope", default)).Error!.Code);
        Assert.Equal("topic-not-found", (await _h.Learn.TopicAsync(_h.LearnerId, BrainVita, "nope", default)).Error!.Code);
        Assert.Equal("topic-not-found", (await _h.Learn.QuizAsync(_h.LearnerId, "nope", "nope", default)).Error!.Code);
    }

    [Fact]
    public async Task Topic_briefs_start_current_then_locked_and_carry_the_design_copy()
    {
        var topic = (await _h.Learn.TopicAsync(_h.LearnerId, BrainVita, NonVerbal, default)).Value;

        Assert.Equal(11, topic.Briefs.Count);
        Assert.Equal("current", topic.Briefs[0].Status);
        Assert.All(topic.Briefs.Skip(1), b => Assert.Equal("locked", b.Status));
        var mirror = topic.Briefs[10];
        Assert.Equal("Mirror and Water Images", mirror.Title);
        Assert.Equal("Lateral inversion", mirror.KeyTerm);
        Assert.Equal("image", mirror.Media!.Kind);
        Assert.Equal(3, mirror.Quiz.Questions);
        Assert.Equal(3, topic.Briefs[0].Quiz.Questions); // a multi-question topic quiz is used by every brief
        Assert.Null(mirror.Quiz.Result);
        Assert.Equal(new CourseCardDto("Non-Verbal Reasoning: Full course + certificate", false, "/learn/brain-vita/non-verbal-reasoning/course"), topic.Course);
        Assert.Equal(new TitleDto("Skill Knook"), topic.Knook);
    }

    [Fact]
    public async Task Single_question_topics_give_earlier_briefs_one_question()
    {
        var topic = (await _h.Learn.TopicAsync(_h.LearnerId, BrainVita, Data, default)).Value;
        Assert.All(topic.Briefs, b => Assert.Equal(1, b.Quiz.Questions));
        Assert.Null(topic.Course);
    }

    [Fact]
    public async Task First_submit_pays_one_xp_per_correct_answer_and_retakes_pay_nothing()
    {
        var quiz = (await _h.Learn.QuizAsync(_h.LearnerId, BrainVita, NonVerbal, default)).Value;
        Assert.Equal("brain-vita~non-verbal-reasoning~0", quiz.Id);
        Assert.False(quiz.AlreadyPassed);
        Assert.Equal("Series Completion", quiz.Title);
        Assert.Equal("nv-q1-o0", quiz.Questions[0].Options[0].Id);

        var answers = Harness.Answers(BrainVita, NonVerbal, 0, right: true);
        answers["nv-q2"] = "nv-q2-o0"; // one wrong, with a pick note
        var first = (await _h.Learn.SubmitAsync(_h.LearnerId, quiz.Id, answers, default)).Value;

        Assert.Equal(2, first.Correct);
        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.Xp);
        Assert.Equal("That’s a mirror image. Water reflects from below.", first.Results[1].PickNote);
        Assert.Equal("nv-q2-o1", first.Results[1].CorrectOptionId);
        Assert.Null(first.Results[0].PickNote);
        Assert.Equal("Nearly! Water flips it upside down.", first.Muni);

        var retake = (await _h.Learn.SubmitAsync(_h.LearnerId, quiz.Id, Harness.Answers(BrainVita, NonVerbal, 0, right: true), default)).Value;
        Assert.Equal(3, retake.Correct);
        Assert.Equal(0, retake.Xp);
        Assert.Equal("Brain on fire today!", retake.Muni);

        var submitted = _h.Store.Published<QuizSubmitted>();
        Assert.Equal(new[] { true, false }, submitted.Select(e => e.FirstAttempt));
        Assert.All(submitted, e => Assert.Equal((NonVerbal, BrainVita, "skillmuni-in"), (e.ModuleSlug, e.CourseSlug, e.TenantId)));
        var completed = Assert.Single(_h.Store.Published<BriefCompleted>());
        Assert.True(completed.FirstCompletion);
        Assert.Equal(BrainVita, completed.CourseSlug);
        Assert.Equal(2, _h.Store.Attempts.Count);
    }

    [Fact]
    public async Task Topic_shows_done_brief_xp_and_last_score_after_a_submit()
    {
        await _h.Learn.SubmitAsync(_h.LearnerId, "brain-vita~non-verbal-reasoning~0", Harness.Answers(BrainVita, NonVerbal, 0, right: false), default);

        var topic = (await _h.Learn.TopicAsync(_h.LearnerId, BrainVita, NonVerbal, default)).Value;
        Assert.Equal("done", topic.Briefs[0].Status);
        Assert.Equal("current", topic.Briefs[1].Status);
        Assert.Equal(new QuizScoreDto(0, 3), topic.Briefs[0].Quiz.Result);
        Assert.Equal(0, topic.Briefs[0].Xp);
        Assert.Equal(1, topic.QuizzesDone);

        var quiz = (await _h.Learn.QuizAsync(_h.LearnerId, BrainVita, NonVerbal, default)).Value;
        Assert.Equal("brain-vita~non-verbal-reasoning~1", quiz.Id);
        Assert.Equal(1, quiz.QuizzesDone);
    }

    [Fact]
    public async Task A_locked_brief_quiz_is_422_and_unknown_quiz_404()
    {
        var locked = await _h.Learn.SubmitAsync(_h.LearnerId, "brain-vita~non-verbal-reasoning~5", new Dictionary<string, string>(), default);
        Assert.Equal("quiz-locked", locked.Error!.Code);
        Assert.Equal("quiz-not-found", (await _h.Learn.SubmitAsync(_h.LearnerId, "brain-vita~non-verbal-reasoning~99", new Dictionary<string, string>(), default)).Error!.Code);
        Assert.Equal("quiz-not-found", (await _h.Learn.SubmitAsync(_h.LearnerId, "garbage", new Dictionary<string, string>(), default)).Error!.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task Finishing_every_brief_completes_the_topic_and_then_the_module()
    {
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(1, await _h.PassNextAsync(BrainVita, Data));
        }

        var module = Assert.Single(_h.Store.Published<ModuleCompleted>());
        Assert.Equal((Data, BrainVita), (module.ModuleSlug, module.CourseSlug));
        Assert.Empty(_h.Store.Published<CourseCompleted>());

        var quiz = (await _h.Learn.QuizAsync(_h.LearnerId, BrainVita, Data, default)).Value;
        Assert.True(quiz.AlreadyPassed);
        Assert.Equal("brain-vita~data-interpretation~5", quiz.Id);

        // A small module: both topics done → course completed once.
        for (var i = 0; i < 3; i++)
        {
            await _h.PassNextAsync("speak-up", "getting-started");
        }

        for (var i = 0; i < 3; i++)
        {
            await _h.PassNextAsync("speak-up", "going-deeper");
        }

        Assert.Equal("speak-up", Assert.Single(_h.Store.Published<CourseCompleted>()).CourseSlug);
        await _h.PassNextAsync("speak-up", "going-deeper"); // practice
        Assert.Single(_h.Store.Published<CourseCompleted>());
    }

    [Fact]
    public async Task Resume_point_follows_the_last_topic_worked_on()
    {
        await _h.PassNextAsync(BrainVita, NonVerbal);
        var module = (await _h.Learn.ModuleAsync(_h.LearnerId, BrainVita, default)).Value;

        Assert.Equal(NonVerbal, module.Resume!.Topic.Slug);
        Assert.Equal("brain-vita~non-verbal-reasoning~1", module.Resume.Brief.Id);
        Assert.Equal("Analogy", module.Resume.Brief.Title);
        Assert.Equal(1, module.QuizzesDone);
        Assert.Equal(3, module.Xp);
    }

    [Fact]
    public async Task Bookmarks_feedback_and_drafts_round_trip()
    {
        const string brief = "brain-vita~non-verbal-reasoning~10";
        Assert.True((await _h.Learn.BookmarkAsync(_h.LearnerId, brief, true, default)).IsSuccess);
        Assert.True((await _h.Learn.FeedbackAsync(_h.LearnerId, brief, false, default)).IsSuccess);
        Assert.Equal("brief-not-found", (await _h.Learn.BookmarkAsync(_h.LearnerId, "brain-vita~nope~0", true, default)).Error!.Code);

        var topic = (await _h.Learn.TopicAsync(_h.LearnerId, BrainVita, NonVerbal, default)).Value;
        Assert.True(topic.Briefs[10].Bookmarked);
        Assert.False(topic.Briefs[10].Helpful);
        Assert.Null(topic.Briefs[0].Helpful);

        const string quizId = "brain-vita~non-verbal-reasoning~0";
        Assert.True((await _h.Learn.SaveDraftAsync(_h.LearnerId, quizId, new Dictionary<string, string> { ["nv-q1"] = "nv-q1-o1" }, default)).IsSuccess);
        Assert.Equal("quiz-not-found", (await _h.Learn.SaveDraftAsync(_h.LearnerId, "x~y~0", new Dictionary<string, string>(), default)).Error!.Code);
        var quiz = (await _h.Learn.QuizAsync(_h.LearnerId, BrainVita, NonVerbal, default)).Value;
        Assert.Equal("nv-q1-o1", quiz.Draft["nv-q1"]);

        await _h.Learn.SubmitAsync(_h.LearnerId, quizId, Harness.Answers(BrainVita, NonVerbal, 0, right: true), default);
        Assert.Empty(_h.Store.Drafts);
    }

    [Fact]
    public async Task Activity_publishes_minutes_up_to_the_daily_cap()
    {
        Assert.True((await _h.Learn.RecordActivityAsync(_h.LearnerId, 15, default)).IsSuccess);
        Assert.Equal(15, Assert.Single(_h.Store.Published<ActivityRecorded>()).Minutes);
        Assert.Equal(new DateOnly(2026, 10, 15), _h.Store.Activity.Single().Day);

        for (var i = 0; i < 30; i++)
        {
            await _h.Learn.RecordActivityAsync(_h.LearnerId, 60, default);
        }

        Assert.Equal(1440, _h.Store.Activity.Single().Minutes);
        Assert.Equal(1440, _h.Store.Published<ActivityRecorded>().Sum(e => e.Minutes));
    }

    [Fact]
    public async Task Muni_line_on_zero_is_the_shake_it_off_copy()
    {
        var result = (await _h.Learn.SubmitAsync(_h.LearnerId, "brain-vita~data-interpretation~0", Harness.Answers(BrainVita, Data, 0, right: false), default)).Value;
        Assert.Equal("Shake it off. Read the why, then go again.", result.Muni);
        Assert.Equal(0, result.Xp);
    }

    [Fact]
    public async Task Erasure_removes_every_row()
    {
        await _h.PassNextAsync(BrainVita, Data);
        await _h.Learn.RecordActivityAsync(_h.LearnerId, 5, default);

        var tombstones = new SkillMuni.Testing.FakeErasureTombstones();
        await new AccountErasedHandler(_h.Store, tombstones).HandleAsync(new AccountErased(_h.LearnerId) { TenantId = "skillmuni-in" }, default);

        Assert.Empty(_h.Store.Progress);
        Assert.Empty(_h.Store.Attempts);
        Assert.Empty(_h.Store.Activity);
        Assert.Contains(_h.LearnerId, tombstones.Erased);
    }
}
