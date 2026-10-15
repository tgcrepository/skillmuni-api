using Assessment.Domain;

namespace Assessment.Application;

// Every query is scoped to the current tenant by the DbContext.

/// <summary>
/// The survey content (pairs, islands, drivers, archetypes). Static seed data today, shipped inside the service; a
/// content admin or CMS can replace the implementation later without touching the use cases.
/// </summary>
public interface ICareerDriversContentProvider
{
    CareerDriversContent Content { get; }
}

public interface ICareerJourneyRepository
{
    /// <summary>Tracked, for a use case that changes it.</summary>
    Task<CareerJourney?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(CareerJourney journey);
}

public interface ICareerAttemptRepository
{
    /// <summary>Every submitted attempt of the learner, oldest first. A handful per learner, so no paging.</summary>
    Task<IReadOnlyList<CareerAttempt>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(CareerAttempt attempt);
}

/// <summary>MISS-08 erasure: every row this service holds for one learner. Safe to run twice.</summary>
public interface IAssessmentDataEraser
{
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

/// <summary>Saves every change in the use case, outbox rows included, in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
