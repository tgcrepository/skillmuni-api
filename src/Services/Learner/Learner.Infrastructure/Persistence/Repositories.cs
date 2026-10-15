using Learner.Application.Abstractions;
using Learner.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learner.Infrastructure.Persistence;

// Repositories return tracked entities when the caller will change them and use AsNoTracking for pure reads.
// LINQ only: EF parameterises every value (CLAUDE.md §5). Each lookup is covered by a unique or plain index.

internal sealed class LearnerRepository(LearnerDbContext db) : ILearnerRepository
{
    public Task<LearnerProfile?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Learners.FirstOrDefaultAsync(l => l.Id == learnerId, cancellationToken);

    public async Task<LearnerProfile> GetOrAddAsync(LearnerProfile candidate, CancellationToken cancellationToken)
    {
        db.Learners.Add(candidate);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return candidate;
        }
        catch (DbUpdateException)
        {
            // A parallel request or the registration event inserted the row first: use that one.
            db.Entry(candidate).State = EntityState.Detached;
            return await GetAsync(candidate.Id, cancellationToken) ?? throw new InvalidOperationException("The learner row could not be created.");
        }
    }

    public void Add(LearnerProfile learner) => db.Learners.Add(learner);
}

internal sealed class QuestAnswersRepository(LearnerDbContext db) : IQuestAnswersRepository
{
    public Task<QuestAnswers?> FindAsync(Guid learnerId, string slug, CancellationToken cancellationToken) =>
        db.QuestAnswers.FirstOrDefaultAsync(a => a.LearnerId == learnerId && a.Slug == slug, cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.QuestAnswers.AsNoTracking()
            .Where(a => a.LearnerId == learnerId)
            .Select(a => new { a.Slug, a.AnswersJson })
            .ToDictionaryAsync(a => a.Slug, a => a.AnswersJson, StringComparer.Ordinal, cancellationToken);

    public Task<DateTime?> LastUpdatedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.QuestAnswers.AsNoTracking().Where(a => a.LearnerId == learnerId).MaxAsync(a => (DateTime?)a.UpdatedAt, cancellationToken);

    public void Add(QuestAnswers answers) => db.QuestAnswers.Add(answers);
}

internal sealed class DraftRepository(LearnerDbContext db) : IDraftRepository
{
    public Task<Draft?> FindAsync(Guid learnerId, string key, CancellationToken cancellationToken) =>
        db.Drafts.FirstOrDefaultAsync(d => d.LearnerId == learnerId && d.Key == key, cancellationToken);

    public void Add(Draft draft) => db.Drafts.Add(draft);
}

internal sealed class LinkedIdentityRepository(LearnerDbContext db) : ILinkedIdentityRepository
{
    public Task<LinkedIdentity?> FindAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.LinkedIdentities.FirstOrDefaultAsync(i => i.LearnerId == learnerId, cancellationToken);

    public void Add(LinkedIdentity identity) => db.LinkedIdentities.Add(identity);

    public void Remove(LinkedIdentity identity) => db.LinkedIdentities.Remove(identity);
}

internal sealed class ProgrammeRepository(LearnerDbContext db) : IProgrammeRepository
{
    public Task<Programme?> FindAsync(string code, CancellationToken cancellationToken) =>
        db.Programmes.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public void Add(Programme programme) => db.Programmes.Add(programme);
}

internal sealed class EnrolmentRepository(LearnerDbContext db) : IEnrolmentRepository
{
    public Task<bool> ExistsAsync(Guid learnerId, string programmeCode, CancellationToken cancellationToken) =>
        db.Enrolments.AsNoTracking().AnyAsync(e => e.LearnerId == learnerId && e.ProgrammeCode == programmeCode, cancellationToken);

    public Task<bool> AnyAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Enrolments.AsNoTracking().AnyAsync(e => e.LearnerId == learnerId, cancellationToken);

    public void Add(Enrolment enrolment) => db.Enrolments.Add(enrolment);
}

internal sealed class FeedbackRepository(LearnerDbContext db) : IFeedbackRepository
{
    public Task<DateTime?> LastXpAwardAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Feedback.AsNoTracking()
            .Where(f => f.LearnerId == learnerId && f.XpAwarded)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => (DateTime?)f.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(Feedback feedback) => db.Feedback.Add(feedback);
}

internal sealed class JobProfileRepository(LearnerDbContext db) : IJobProfileRepository
{
    public Task<JobProfileSettings?> FindAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.JobProfiles.FirstOrDefaultAsync(j => j.LearnerId == learnerId, cancellationToken);

    public void Add(JobProfileSettings settings) => db.JobProfiles.Add(settings);
}

internal sealed class TrophyFileRepository(LearnerDbContext db) : ITrophyFileRepository
{
    public Task<int> CountAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.TrophyFiles.AsNoTracking().CountAsync(f => f.LearnerId == learnerId, cancellationToken);

    public void Add(TrophyFile file) => db.TrophyFiles.Add(file);
}

/// <summary>MISS-08. Set-based deletes, one statement per table; running it twice deletes nothing the second time.</summary>
internal sealed class LearnerDataEraser(LearnerDbContext db) : ILearnerDataEraser
{
    public async Task<IReadOnlyList<string>> ListBlobsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var photo = await db.Learners.AsNoTracking()
            .Where(l => l.Id == learnerId && l.PhotoBlob != null)
            .Select(l => l.PhotoBlob!)
            .ToListAsync(cancellationToken);
        var files = await db.TrophyFiles.AsNoTracking()
            .Where(f => f.LearnerId == learnerId)
            .Select(f => f.BlobName)
            .ToListAsync(cancellationToken);
        var cvs = await db.JobProfiles.AsNoTracking()
            .Where(j => j.LearnerId == learnerId && j.CvBlob != null)
            .Select(j => j.CvBlob!)
            .ToListAsync(cancellationToken);
        return [.. photo, .. files, .. cvs];
    }

    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>

        // The retrying execution strategy needs the transaction inside it, so a retry replays the whole unit.
        db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.QuestAnswers.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.Drafts.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.LinkedIdentities.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.Enrolments.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.Feedback.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.TrophyFiles.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.JobProfiles.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
            await db.Learners.Where(x => x.Id == learnerId).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
}
