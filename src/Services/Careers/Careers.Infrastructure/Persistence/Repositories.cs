using Careers.Application.Abstractions;
using Careers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Careers.Infrastructure.Persistence;

internal sealed class CareerLearnerRepository(CareersDbContext db) : ICareerLearnerRepository
{
    public Task<CareerLearner?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Learners.FirstOrDefaultAsync(l => l.Id == learnerId, cancellationToken);

    public void Add(CareerLearner learner) => db.Learners.Add(learner);
}

internal sealed class SavedJobRepository(CareersDbContext db) : ISavedJobRepository
{
    public async Task<IReadOnlyDictionary<string, DateTime>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.SavedJobs.AsNoTracking().Where(s => s.LearnerId == learnerId)
            .ToDictionaryAsync(s => s.JobId, s => s.SavedAt, StringComparer.Ordinal, cancellationToken);

    public Task<SavedJob?> FindAsync(Guid learnerId, string jobId, CancellationToken cancellationToken) =>
        db.SavedJobs.FirstOrDefaultAsync(s => s.LearnerId == learnerId && s.JobId == jobId, cancellationToken);

    public void Add(SavedJob saved) => db.SavedJobs.Add(saved);

    public void Remove(SavedJob saved) => db.SavedJobs.Remove(saved);
}

internal sealed class ApplicationRepository(CareersDbContext db) : IApplicationRepository
{
    public async Task<IReadOnlyList<JobApplication>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.Applications.AsNoTracking().Where(a => a.LearnerId == learnerId).OrderByDescending(a => a.AppliedAt).ToListAsync(cancellationToken);

    public Task<JobApplication?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        db.Applications.FirstOrDefaultAsync(a => a.LearnerId == learnerId && a.Id == id, cancellationToken);

    public Task<JobApplication?> LatestForJobAsync(Guid learnerId, string jobId, CancellationToken cancellationToken) =>
        db.Applications.AsNoTracking()
            .Where(a => a.LearnerId == learnerId && a.JobId == jobId && a.Source == ApplicationSource.Apply)
            .OrderByDescending(a => a.AppliedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> FestOfferExistsAsync(Guid learnerId, string festOfferKey, CancellationToken cancellationToken) =>
        db.Applications.AsNoTracking().AnyAsync(a => a.LearnerId == learnerId && a.FestOfferKey == festOfferKey, cancellationToken);

    public void Add(JobApplication application) => db.Applications.Add(application);
}

internal sealed class CvFileRepository(CareersDbContext db) : ICvFileRepository
{
    public async Task<IReadOnlyList<CvFile>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.CvFiles.AsNoTracking().Where(f => f.LearnerId == learnerId).OrderByDescending(f => f.UploadedAt).ToListAsync(cancellationToken);

    public Task<CvFile?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        db.CvFiles.AsNoTracking().FirstOrDefaultAsync(f => f.LearnerId == learnerId && f.Id == id, cancellationToken);

    public void Add(CvFile file) => db.CvFiles.Add(file);
}

internal sealed class InvitationRepository(CareersDbContext db) : IInvitationRepository
{
    public async Task<IReadOnlyList<Invitation>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.Invitations.AsNoTracking().Where(i => i.LearnerId == learnerId).OrderBy(i => i.CreatedAt).ToListAsync(cancellationToken);

    public Task<Invitation?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        db.Invitations.FirstOrDefaultAsync(i => i.LearnerId == learnerId && i.Id == id, cancellationToken);

    public void Add(Invitation invitation) => db.Invitations.Add(invitation);
}

internal sealed class FestBookingRepository(CareersDbContext db) : IFestBookingRepository
{
    public async Task<IReadOnlyList<FestBooking>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.FestBookings.AsNoTracking().Include(b => b.Picks).Where(b => b.LearnerId == learnerId).OrderBy(b => b.CreatedAt).ToListAsync(cancellationToken);

    public Task<FestBooking?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        db.FestBookings.Include(b => b.Picks).FirstOrDefaultAsync(b => b.LearnerId == learnerId && b.Id == id, cancellationToken);

    public Task<FestBooking?> FindByFestAsync(Guid learnerId, string festId, CancellationToken cancellationToken) =>
        db.FestBookings.Include(b => b.Picks).FirstOrDefaultAsync(b => b.LearnerId == learnerId && b.FestId == festId, cancellationToken);

    public async Task<IReadOnlyDictionary<(string EmployerId, DateOnly Date, string Time), int>> SlotUseAsync(string festId, CancellationToken cancellationToken)
    {
        var rows = await db.FestPicks.AsNoTracking()
            .Where(p => p.FestId == festId)
            .GroupBy(p => new { p.EmployerId, p.Date, p.Time })
            .Select(g => new { g.Key.EmployerId, g.Key.Date, g.Key.Time, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => (r.EmployerId, r.Date, r.Time), r => r.Count);
    }

    public async Task<FestUsage> UsageAsync(IReadOnlyCollection<string> festIds, CancellationToken cancellationToken)
    {
        if (festIds.Count == 0)
        {
            return FestUsage.None;
        }

        var ids = festIds.ToList();
        var bookings = await db.FestBookings.AsNoTracking()
            .Where(b => ids.Contains(b.FestId))
            .GroupBy(b => b.FestId)
            .Select(g => new { FestId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FestId, x => x.Count, StringComparer.Ordinal, cancellationToken);
        var interviews = await db.FestPicks.AsNoTracking()
            .Where(p => ids.Contains(p.FestId))
            .GroupBy(p => new { p.FestId, p.EmployerId })
            .Select(g => new { g.Key.FestId, g.Key.EmployerId, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return new FestUsage(bookings, interviews.ToDictionary(x => (x.FestId, x.EmployerId), x => x.Count));
    }

    public void Add(FestBooking booking) => db.FestBookings.Add(booking);

    public void Remove(FestBooking booking) => db.FestBookings.Remove(booking);
}

internal sealed class FestActivityRepository(CareersDbContext db) : IFestActivityRepository
{
    public async Task<IReadOnlySet<string>> ListSavedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        (await db.SavedFests.AsNoTracking().Where(s => s.LearnerId == learnerId).Select(s => s.FestId).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

    public Task<SavedFest?> FindSavedAsync(Guid learnerId, string festId, CancellationToken cancellationToken) =>
        db.SavedFests.FirstOrDefaultAsync(s => s.LearnerId == learnerId && s.FestId == festId, cancellationToken);

    public void Add(SavedFest saved) => db.SavedFests.Add(saved);

    public void Remove(SavedFest saved) => db.SavedFests.Remove(saved);

    public Task<FestWaitlistEntry?> FindWaitlistAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken) =>
        db.FestWaitlist.FirstOrDefaultAsync(w => w.LearnerId == learnerId && w.FestId == festId && w.EmployerId == employerId, cancellationToken);

    public Task<int> CountWaitlistAsync(string festId, string employerId, DateTime? joinedAt, CancellationToken cancellationToken)
    {
        var queue = db.FestWaitlist.AsNoTracking().Where(w => w.FestId == festId && w.EmployerId == employerId);
        if (joinedAt is { } at)
        {
            queue = queue.Where(w => w.JoinedAt <= at);
        }

        return queue.CountAsync(cancellationToken);
    }

    public void Add(FestWaitlistEntry entry) => db.FestWaitlist.Add(entry);

    public async Task<IReadOnlyList<FestOutcome>> ListOutcomesAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.FestOutcomes.AsNoTracking().Where(o => o.LearnerId == learnerId).ToListAsync(cancellationToken);

    public Task<FestOutcome?> FindOutcomeAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken) =>
        db.FestOutcomes.FirstOrDefaultAsync(o => o.LearnerId == learnerId && o.FestId == festId && o.EmployerId == employerId, cancellationToken);

    public void Add(FestOutcome outcome) => db.FestOutcomes.Add(outcome);

    public void Remove(FestOutcome outcome) => db.FestOutcomes.Remove(outcome);

    public async Task<IReadOnlyList<FestRating>> ListRatingsAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.FestRatings.AsNoTracking().Where(r => r.LearnerId == learnerId).ToListAsync(cancellationToken);

    public Task<FestRating?> FindRatingAsync(Guid learnerId, string festId, CancellationToken cancellationToken) =>
        db.FestRatings.FirstOrDefaultAsync(r => r.LearnerId == learnerId && r.FestId == festId, cancellationToken);

    public void Add(FestRating rating) => db.FestRatings.Add(rating);
}

internal sealed class MatchStateRepository(CareersDbContext db) : IMatchStateRepository
{
    public async Task<IReadOnlyDictionary<string, string>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.MatchStates.AsNoTracking().Where(m => m.LearnerId == learnerId)
            .ToDictionaryAsync(m => m.MatchId, m => m.Status, StringComparer.Ordinal, cancellationToken);

    public Task<MatchState?> FindAsync(Guid learnerId, string matchId, CancellationToken cancellationToken) =>
        db.MatchStates.FirstOrDefaultAsync(m => m.LearnerId == learnerId && m.MatchId == matchId, cancellationToken);

    public void Add(MatchState state) => db.MatchStates.Add(state);
}

/// <summary>MISS-08. Set-based deletes in one transaction (inside the retrying execution strategy).</summary>
internal sealed class CareersDataEraser(CareersDbContext db) : ICareersDataEraser
{
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.FestPicks.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.FestBookings.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.FestWaitlist.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.FestOutcomes.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.FestRatings.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.SavedFests.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.SavedJobs.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Applications.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.CvFiles.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Invitations.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.MatchStates.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Learners.Where(x => x.Id == learnerId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
}
