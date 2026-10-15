using Muni.Domain;

namespace Muni.Application;

// Every query is scoped to the current tenant by the DbContext.

public interface IMuniLearnerRepository
{
    /// <summary>Tracked, for event handlers that change it.</summary>
    Task<MuniLearner?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Read-only, for replies.</summary>
    Task<MuniLearner?> FindAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(MuniLearner learner);
}

/// <summary>A stored chat line, projected for the history screen.</summary>
public sealed record ChatLine(Guid Id, Guid ConversationId, string From, string Text, DateTime CreatedAt);

public interface IChatRepository
{
    /// <summary>The learner's most recent line, to decide whether a new question continues that conversation.</summary>
    Task<ChatLine?> LastAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Newest first, at most <paramref name="limit"/> lines since <paramref name="since"/>.</summary>
    Task<IReadOnlyList<ChatLine>> RecentAsync(Guid learnerId, DateTime since, int limit, CancellationToken cancellationToken);

    void Add(ChatMessage message);
}

public interface IRatingRepository
{
    Task<ReplyRating?> FindAsync(Guid learnerId, string messageRef, CancellationToken cancellationToken);

    void Add(ReplyRating rating);

    void Remove(ReplyRating rating);
}

/// <summary>MISS-08 erasure: every row this service holds for one learner. Safe to run twice.</summary>
public interface IMuniDataEraser
{
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

/// <summary>Retention (BRD §9, <c>Muni:HistoryDays</c>): runs across all tenants from the background worker.</summary>
public interface IChatRetention
{
    /// <summary>Deletes up to <paramref name="batch"/> chat lines and ratings older than <paramref name="cutoff"/>; returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, int batch, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

// ---- Data owned by other services (read through their APIs, never their schemas) ----

public sealed record ProfileStep(string Label, int Part);

public sealed record ProfileQuest(int Id, string Slug, string Name, IReadOnlyList<ProfileStep> Left);

/// <summary>Learner's A-262 job-profile completeness, quest by quest.</summary>
public sealed record ProfileProgress(int Percent, IReadOnlyList<ProfileQuest> Quests);

/// <summary>Learner service, internal API <c>GET internal/learners/{id}/profile-progress</c>.</summary>
public interface IProfileProgressClient
{
    /// <returns>Null when Learner can't be reached; replies then leave profile gaps out.</returns>
    Task<ProfileProgress?> GetAsync(Guid learnerId, CancellationToken cancellationToken);
}

/// <summary>
/// The caller's own private details (mobile, email). Learner owns them; Muni asks Learner's
/// <c>POST profile/reveal</c> on the learner's behalf, with the learner's own session.
/// </summary>
public interface IPrivateFieldClient
{
    /// <returns>The unmasked value ("" when the learner has none), or null when Learner can't be reached.</returns>
    Task<string?> RevealAsync(string field, CancellationToken cancellationToken);
}

// ---- The responder (MISS-14) ----

public sealed record MuniQuestion(string Text, string? Intent);

public sealed record MuniGap(string Quest, string Left, string Url);

/// <summary>Everything a reply may mention about the learner (muni-knowledge.ts <c>Me</c>).</summary>
public sealed record LearnerSnapshot(
    Guid LearnerId,
    string First,
    string FullName,
    string Initials,
    bool Fresh,
    int Xp,
    int Level,
    string Rank,
    int ToNext,
    int QuestsDone,
    int ProfilePercent,
    IReadOnlyList<MuniGap> Gaps,
    string? Archetype,
    int Streak,
    DateTime? JoinedAt);

/// <summary>A reply before it is stored. <see cref="Topic"/> is the catalogue entry that answered (or <c>fallback</c>).</summary>
public sealed record MuniAnswer(string Topic, IReadOnlyList<MuniBlock> Blocks, IReadOnlyList<string> Chips);

/// <summary>
/// Turns a question into a reply. Phase 1 ships the mock's keyword-intent catalogue
/// (<see cref="Responders.KeywordMuniResponder"/>); an LLM-backed responder can replace it without touching the API.
/// </summary>
public interface IMuniResponder
{
    Task<MuniAnswer> AnswerAsync(MuniQuestion question, LearnerSnapshot me, CancellationToken cancellationToken);
}
