namespace Learning.Domain;

/// <summary>
/// One learner's state on one brief: bookmark, "was this helpful?", and its quiz. A brief is <b>done</b> (passed) from the
/// first time its quiz is submitted, whatever the score (the mock's progress rule); that first submit is the only one
/// that pays XP (BR-106). Later submits are practice and only update the last result.
/// </summary>
public sealed class BriefProgress : Entity
{
    private BriefProgress()
    {
    }

    public Guid LearnerId { get; private set; }

    /// <summary><c>{module}~{topic}~{index}</c>, the id the app uses for the brief and its quiz.</summary>
    public string BriefId { get; private set; } = string.Empty;

    public string ModuleSlug { get; private set; } = string.Empty;

    public string TopicSlug { get; private set; } = string.Empty;

    /// <summary>0-based position in the topic.</summary>
    public int BriefIndex { get; private set; }

    public bool Bookmarked { get; private set; }

    public bool? Helpful { get; private set; }

    /// <summary>First quiz submit. Null until then.</summary>
    public DateTime? PassedAt { get; private set; }

    /// <summary>XP the first submit earned (+1 per correct answer).</summary>
    public int Xp { get; private set; }

    public int? LastCorrect { get; private set; }

    public int? LastTotal { get; private set; }

    public DateTime? LastSubmittedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool Passed => PassedAt is not null;

    public static BriefProgress Start(string tenantId, Guid learnerId, BriefKey key, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        BriefId = key.ToString(),
        ModuleSlug = key.Module,
        TopicSlug = key.Topic,
        BriefIndex = key.Index,
        UpdatedAt = now,
    };

    public void Bookmark(bool on, DateTime now)
    {
        Bookmarked = on;
        UpdatedAt = now;
    }

    public void RateHelpful(bool? helpful, DateTime now)
    {
        Helpful = helpful;
        UpdatedAt = now;
    }

    /// <summary>Records a checked quiz.</summary>
    /// <returns>The XP this submit earns: the correct count on the first submit, 0 afterwards.</returns>
    public int RecordQuiz(int correct, int total, DateTime now)
    {
        var first = !Passed;
        if (first)
        {
            PassedAt = now;
            Xp = correct;
        }

        LastCorrect = correct;
        LastTotal = total;
        LastSubmittedAt = now;
        UpdatedAt = now;
        return first ? correct : 0;
    }
}

/// <summary>Every quiz check, first or practice (history; the XP is what the response showed).</summary>
public sealed class QuizAttempt : Entity
{
    private QuizAttempt()
    {
    }

    public Guid LearnerId { get; private set; }

    public string QuizId { get; private set; } = string.Empty;

    public int Correct { get; private set; }

    public int Total { get; private set; }

    public int Xp { get; private set; }

    public bool FirstAttempt { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    public static QuizAttempt Record(string tenantId, Guid learnerId, string quizId, int correct, int total, int xp, bool firstAttempt, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        QuizId = quizId,
        Correct = correct,
        Total = total,
        Xp = xp,
        FirstAttempt = firstAttempt,
        SubmittedAt = now,
    };
}

/// <summary>Autosaved picks for a quiz not yet submitted (question id → option id, as JSON).</summary>
public sealed class QuizDraft : Entity
{
    private QuizDraft()
    {
    }

    public Guid LearnerId { get; private set; }

    public string QuizId { get; private set; } = string.Empty;

    public string AnswersJson { get; private set; } = "{}";

    public DateTime UpdatedAt { get; private set; }

    public static QuizDraft Start(string tenantId, Guid learnerId, string quizId, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, QuizId = quizId, UpdatedAt = now };

    public void Save(string answersJson, DateTime now)
    {
        AnswersJson = answersJson;
        UpdatedAt = now;
    }
}

/// <summary>
/// The Lobby's "Your first moves" card (A-233, A-266). "Learn" is derived from brief progress; "assessment" comes from
/// Assessment's event; "jobs" is done by opening My Jobs from the card. The +50 bonus is collected once (BR-107).
/// </summary>
public sealed class LobbyState : Entity
{
    private LobbyState()
    {
    }

    public Guid LearnerId { get; private set; }

    public DateTime? AssessmentDoneAt { get; private set; }

    public DateTime? JobsVisitedAt { get; private set; }

    public DateTime? FirstMovesClaimedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool Claimed => FirstMovesClaimedAt is not null;

    public static LobbyState Start(string tenantId, Guid learnerId, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, UpdatedAt = now };

    public void MarkAssessmentDone(DateTime now)
    {
        AssessmentDoneAt ??= now;
        UpdatedAt = now;
    }

    public void MarkJobsVisited(DateTime now)
    {
        JobsVisitedAt ??= now;
        UpdatedAt = now;
    }

    /// <returns>True the first time; false when the bonus was already collected.</returns>
    public bool Claim(DateTime now)
    {
        if (Claimed)
        {
            return false;
        }

        FirstMovesClaimedAt = now;
        UpdatedAt = now;
        return true;
    }
}

/// <summary>Learning minutes the app reported on one local day (MISS-04), capped per day.</summary>
public sealed class ActivityDay : Entity
{
    private ActivityDay()
    {
    }

    public Guid LearnerId { get; private set; }

    public DateOnly Day { get; private set; }

    public int Minutes { get; private set; }

    public static ActivityDay On(string tenantId, Guid learnerId, DateOnly day) =>
        new() { TenantId = tenantId, LearnerId = learnerId, Day = day };

    /// <returns>The minutes actually added (0 once the day's cap is reached).</returns>
    public int Add(int minutes, int dailyCap)
    {
        var added = Math.Clamp(Math.Min(minutes, dailyCap - Minutes), 0, int.MaxValue);
        Minutes += added;
        return added;
    }
}
