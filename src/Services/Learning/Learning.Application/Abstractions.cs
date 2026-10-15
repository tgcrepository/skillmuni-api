using Learning.Domain;

namespace Learning.Application;

// Every query is scoped to the current tenant by the DbContext.

/// <summary>
/// The learning catalogue: modules, topics, briefs, quizzes (with answers and explanations) and the Lobby shelves.
/// Static seed data today (copied from the app's mocks); a CMS-backed provider can replace it.
/// </summary>
public interface ILearningContent
{
    /// <summary>Every module, in catalogue order.</summary>
    IReadOnlyList<ModuleContent> Modules { get; }

    ModuleContent? Module(string slug);

    IReadOnlyList<Pathway> Pathways { get; }

    IReadOnlyList<Knook> Knooks { get; }
}

/// <summary>A passed brief (its quiz submitted at least once), as the progress screens need it.</summary>
public sealed record PassedBrief(string ModuleSlug, string TopicSlug, int BriefIndex, int Xp, DateTime LastSubmittedAt);

public interface IBriefProgressRepository
{
    /// <summary>The learner's rows in one module, read-only.</summary>
    Task<IReadOnlyList<BriefProgress>> ListModuleAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken);

    /// <summary>The learner's rows in one module, tracked for an update.</summary>
    Task<IReadOnlyList<BriefProgress>> ListModuleForUpdateAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken);

    /// <summary>One row, tracked; null when the learner never touched the brief.</summary>
    Task<BriefProgress?> GetAsync(Guid learnerId, string briefId, CancellationToken cancellationToken);

    /// <summary>Every passed brief of the learner, across modules (projection).</summary>
    Task<IReadOnlyList<PassedBrief>> ListPassedAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<bool> AnyPassedAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(BriefProgress progress);
}

public interface IQuizRepository
{
    /// <summary>Tracked; null when there is no draft.</summary>
    Task<QuizDraft?> GetDraftAsync(Guid learnerId, string quizId, CancellationToken cancellationToken);

    void Add(QuizDraft draft);

    void Remove(QuizDraft draft);

    void Add(QuizAttempt attempt);
}

public interface ILobbyRepository
{
    /// <summary>Tracked; null for a learner who has no Lobby state yet.</summary>
    Task<LobbyState?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(LobbyState state);
}

public interface IActivityRepository
{
    /// <summary>Tracked; null when nothing was recorded that day.</summary>
    Task<ActivityDay?> GetAsync(Guid learnerId, DateOnly day, CancellationToken cancellationToken);

    void Add(ActivityDay day);
}

/// <summary>MISS-08 erasure: every row this service holds for one learner. Safe to run twice.</summary>
public interface ILearnerDataEraser
{
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    /// <summary>One transaction: the changes and their outbox rows (CLAUDE.md §5).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
