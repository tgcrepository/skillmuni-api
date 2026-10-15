namespace Assessment.Domain;

/// <summary>Every row has a UUID key and belongs to one tenant (CLAUDE.md §5, §6).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public string TenantId { get; protected set; } = string.Empty;
}

/// <summary>
/// One learner's Career Drivers state: the attempt in progress (answers auto-saved after every tap, the pair to show
/// next, islands already cleared and the XP shown for them) plus the reminder flag and how many attempts were
/// submitted. The id is the learner's account id.
/// </summary>
public sealed class CareerJourney : Entity
{
    private CareerJourney()
    {
    }

    public Guid LearnerId => Id;

    /// <summary>One code per pair (<see cref="Answer.ToCode"/>).</summary>
    public string AnswersCode { get; private set; } = string.Empty;

    /// <summary>0-based index of the pair to show next.</summary>
    public int Current { get; private set; }

    /// <summary>XP shown for this attempt so far (islands cleared). 0 on retakes.</summary>
    public int Xp { get; private set; }

    /// <summary>Bit n−1 set = island n already cleared in this attempt (each island counts once per attempt).</summary>
    public int IslandsCleared { get; private set; }

    /// <summary>"Remind me tomorrow" (one nudge at 6 PM, A-218). On by default, as in the app.</summary>
    public bool Remind { get; private set; } = true;

    public DateTime? RemindChangedAt { get; private set; }

    /// <summary>Submitted attempts.</summary>
    public int Attempts { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Only the first attempt earns XP; retakes and restarts after it don't (Figma S2, BR-104/105).</summary>
    public bool EarnsXp => Attempts == 0;

    /// <summary>The number the attempt in progress will get when submitted.</summary>
    public int AttemptInProgress => Attempts + 1;

    public static CareerJourney Start(Guid learnerId, string tenantId, DateTime now) => new()
    {
        Id = learnerId,
        TenantId = tenantId,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public IReadOnlyList<Answer> Answers(int pairCount) => Answer.Decode(AnswersCode, pairCount);

    public bool HasCleared(int island) => (IslandsCleared & Bit(island)) != 0;

    /// <summary>
    /// Auto-save (absolute values, so a replayed save changes nothing, BR-133). Returns the islands this save cleared for
    /// the first time in the attempt; the last island is never cleared here, it is paid on submit (A-309).
    /// </summary>
    public IReadOnlyList<int> Save(IReadOnlyList<Answer> answers, int current, CareerDriversContent content, int xpPerIsland, DateTime now)
    {
        AnswersCode = Answer.Encode(answers);
        Current = Math.Clamp(current, 0, content.Pairs.Count);
        UpdatedAt = now;
        return ClearIslands(answers, content, xpPerIsland, includeLast: false);
    }

    /// <summary>Starts this attempt again from pair 1. Islands already cleared stay cleared, so they never pay twice.</summary>
    public void Restart(DateTime now)
    {
        AnswersCode = string.Empty;
        Current = 0;
        UpdatedAt = now;
    }

    /// <summary>
    /// A new attempt after a submitted one: answers, islands and XP start over and nothing more is earned. With no
    /// submitted attempt yet it is a restart, so the first attempt's XP is never forfeited.
    /// </summary>
    public void Retake(DateTime now)
    {
        if (Attempts == 0)
        {
            Restart(now);
            return;
        }

        AnswersCode = string.Empty;
        Current = 0;
        Xp = 0;
        IslandsCleared = 0;
        UpdatedAt = now;
    }

    /// <summary>
    /// Closes the attempt with all pairs answered. Islands 1–3 not cleared yet (answers sent straight to submit) are
    /// cleared now; the last island pays <paramref name="xpOnSubmit"/>. Returns those islands and the attempt's total XP.
    /// </summary>
    public (IReadOnlyList<int> Cleared, int AttemptXp, bool FirstAttempt) Submit(
        IReadOnlyList<Answer> answers,
        CareerDriversContent content,
        int xpPerIsland,
        int xpOnSubmit,
        DateTime now)
    {
        var firstAttempt = EarnsXp;
        var cleared = ClearIslands(answers, content, xpPerIsland, includeLast: false);
        var attemptXp = Xp + (firstAttempt ? xpOnSubmit : 0);

        Attempts++;
        AnswersCode = string.Empty;
        Current = 0;
        Xp = 0;
        IslandsCleared = 0;
        UpdatedAt = now;
        return (cleared, attemptXp, firstAttempt);
    }

    public void SetRemind(bool remind, DateTime now)
    {
        Remind = remind;
        RemindChangedAt = now;
        UpdatedAt = now;
    }

    private List<int> ClearIslands(IReadOnlyList<Answer> answers, CareerDriversContent content, int xpPerIsland, bool includeLast)
    {
        var cleared = new List<int>();
        foreach (var island in content.Islands.OrderBy(i => i.N))
        {
            if ((!includeLast && island.N == content.LastIsland) || HasCleared(island.N) || !island.IsComplete(answers))
            {
                continue;
            }

            IslandsCleared |= Bit(island.N);
            if (EarnsXp)
            {
                Xp += xpPerIsland;
            }

            cleared.Add(island.N);
        }

        return cleared;
    }

    private static int Bit(int island) => island is >= 1 and <= 30 ? 1 << (island - 1) : 0;
}

/// <summary>A submitted Career Drivers attempt: the scores, the archetype matched at the time and the XP it earned.</summary>
public sealed class CareerAttempt : Entity
{
    private CareerAttempt()
    {
    }

    public Guid LearnerId { get; private set; }

    /// <summary>1-based, per learner.</summary>
    public int Number { get; private set; }

    /// <summary>The learner's local date (market time zone).</summary>
    public DateOnly TakenOn { get; private set; }

    public DriverScores Scores { get; private set; } = new(new int[9]);

    public string ArchetypeId { get; private set; } = string.Empty;

    /// <summary>All the XP the attempt showed: 100 on the first attempt (4 × 25), 0 for retakes.</summary>
    public int XpEarned { get; private set; }

    /// <summary>The 36 answers as submitted, so results can be re-scored if the survey's rules change.</summary>
    public string AnswersCode { get; private set; } = string.Empty;

    public DateTime SubmittedAt { get; private set; }

    public static CareerAttempt Record(
        Guid learnerId,
        string tenantId,
        int number,
        DateOnly takenOn,
        DriverScores scores,
        string archetypeId,
        int xpEarned,
        IReadOnlyList<Answer> answers,
        DateTime now) => new()
        {
            TenantId = tenantId,
            LearnerId = learnerId,
            Number = number,
            TakenOn = takenOn,
            Scores = scores,
            ArchetypeId = archetypeId,
            XpEarned = xpEarned,
            AnswersCode = Answer.Encode(answers),
            SubmittedAt = now,
        };
}
