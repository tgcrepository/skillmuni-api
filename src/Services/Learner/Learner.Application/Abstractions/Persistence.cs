using Learner.Domain;

namespace Learner.Application.Abstractions;

// Every query is scoped to the current tenant by the DbContext's global filters.

public interface ILearnerRepository
{
    Task<LearnerProfile?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="candidate"/> at once, or returns the row a concurrent request or event inserted first.
    /// Call it before any other change in the use case (it saves).
    /// </summary>
    Task<LearnerProfile> GetOrAddAsync(LearnerProfile candidate, CancellationToken cancellationToken);

    /// <summary>Tracked insert, saved with the rest of the unit of work (event handlers).</summary>
    void Add(LearnerProfile learner);
}

public interface IQuestAnswersRepository
{
    Task<QuestAnswers?> FindAsync(Guid learnerId, string slug, CancellationToken cancellationToken);

    /// <summary>Slug → answers JSON for every quest the learner has saved (one query, for A-262).</summary>
    Task<IReadOnlyDictionary<string, string>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>When any of the learner's quest answers last changed (the Job Profile's "updated").</summary>
    Task<DateTime?> LastUpdatedAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(QuestAnswers answers);
}

public interface IDraftRepository
{
    Task<Draft?> FindAsync(Guid learnerId, string key, CancellationToken cancellationToken);

    void Add(Draft draft);
}

public interface ILinkedIdentityRepository
{
    Task<LinkedIdentity?> FindAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(LinkedIdentity identity);

    void Remove(LinkedIdentity identity);
}

public interface IProgrammeRepository
{
    Task<Programme?> FindAsync(string code, CancellationToken cancellationToken);

    void Add(Programme programme);
}

public interface IEnrolmentRepository
{
    Task<bool> ExistsAsync(Guid learnerId, string programmeCode, CancellationToken cancellationToken);

    /// <summary>Whether the learner has joined any programme (BR-113 pays once per learner).</summary>
    Task<bool> AnyAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(Enrolment enrolment);
}

public interface IFeedbackRepository
{
    /// <summary>When this learner last earned XP for feedback, if ever.</summary>
    Task<DateTime?> LastXpAwardAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(Feedback feedback);
}

public interface IJobProfileRepository
{
    Task<JobProfileSettings?> FindAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(JobProfileSettings settings);

    /// <summary>
    /// The learner's row (tracked), inserting <paramref name="candidate"/> first when there is none. Safe when two
    /// handlers or requests create it at once: one insert wins and both get the same row (no duplicate-key error).
    /// </summary>
    Task<JobProfileSettings> GetOrCreateAsync(JobProfileSettings candidate, CancellationToken cancellationToken);
}

public interface ITrophyFileRepository
{
    Task<int> CountAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(TrophyFile file);
}

/// <summary>MISS-08 erasure: every row and file this service holds for one learner.</summary>
public interface ILearnerDataEraser
{
    /// <summary>Photo and certificate blobs, deleted before the rows so a retry can still find them.</summary>
    Task<IReadOnlyList<string>> ListBlobsAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Deletes the rows straight away (not part of the unit of work). Safe to run twice.</summary>
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

/// <summary>Saves every change made in the use case, outbox rows included, in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
