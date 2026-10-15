using Assessment.Application;
using Assessment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Assessment.Infrastructure.Persistence;

internal sealed class CareerJourneyRepository(AssessmentDbContext db) : ICareerJourneyRepository
{
    public async Task<CareerJourney?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Journeys.Local.FirstOrDefault(j => j.Id == learnerId)
        ?? await db.Journeys.FirstOrDefaultAsync(j => j.Id == learnerId, cancellationToken);

    public async Task<bool> ExistsAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Journeys.Local.Any(j => j.Id == learnerId)
        || await db.Journeys.AsNoTracking().AnyAsync(j => j.Id == learnerId, cancellationToken);

    public void Add(CareerJourney journey) => db.Journeys.Add(journey);
}

internal sealed class CareerAttemptRepository(AssessmentDbContext db) : ICareerAttemptRepository
{
    public async Task<IReadOnlyList<CareerAttempt>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.Attempts.AsNoTracking()
            .Where(a => a.LearnerId == learnerId)
            .OrderBy(a => a.Number)
            .ToListAsync(cancellationToken);

    public void Add(CareerAttempt attempt) => db.Attempts.Add(attempt);
}

internal sealed class AssessmentDataEraser(AssessmentDbContext db) : IAssessmentDataEraser
{
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Attempts.Where(a => a.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Journeys.Where(j => j.Id == learnerId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
}
