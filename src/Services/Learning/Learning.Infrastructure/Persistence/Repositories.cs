using Learning.Application;
using Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure.Persistence;

internal sealed class BriefProgressRepository(LearningDbContext db) : IBriefProgressRepository
{
    public async Task<IReadOnlyList<BriefProgress>> ListModuleAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken) =>
        await db.BriefProgress.AsNoTracking().Where(p => p.LearnerId == learnerId && p.ModuleSlug == moduleSlug).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BriefProgress>> ListModuleForUpdateAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken) =>
        await db.BriefProgress.Where(p => p.LearnerId == learnerId && p.ModuleSlug == moduleSlug).ToListAsync(cancellationToken);

    public Task<BriefProgress?> GetAsync(Guid learnerId, string briefId, CancellationToken cancellationToken) =>
        db.BriefProgress.FirstOrDefaultAsync(p => p.LearnerId == learnerId && p.BriefId == briefId, cancellationToken);

    public async Task<IReadOnlyList<PassedBrief>> ListPassedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.BriefProgress.AsNoTracking()
            .Where(p => p.LearnerId == learnerId && p.PassedAt != null)
            .Select(p => new PassedBrief(p.ModuleSlug, p.TopicSlug, p.BriefIndex, p.Xp, p.LastSubmittedAt ?? p.PassedAt!.Value))
            .ToListAsync(cancellationToken);

    public Task<bool> AnyPassedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.BriefProgress.AsNoTracking().AnyAsync(p => p.LearnerId == learnerId && p.PassedAt != null, cancellationToken);

    public void Add(BriefProgress progress) => db.BriefProgress.Add(progress);
}

internal sealed class QuizRepository(LearningDbContext db) : IQuizRepository
{
    public Task<QuizDraft?> GetDraftAsync(Guid learnerId, string quizId, CancellationToken cancellationToken) =>
        db.QuizDrafts.FirstOrDefaultAsync(d => d.LearnerId == learnerId && d.QuizId == quizId, cancellationToken);

    public void Add(QuizDraft draft) => db.QuizDrafts.Add(draft);

    public void Remove(QuizDraft draft) => db.QuizDrafts.Remove(draft);

    public void Add(QuizAttempt attempt) => db.QuizAttempts.Add(attempt);
}

internal sealed class LobbyRepository(LearningDbContext db) : ILobbyRepository
{
    public Task<LobbyState?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.LobbyStates.FirstOrDefaultAsync(s => s.LearnerId == learnerId, cancellationToken);

    public void Add(LobbyState state) => db.LobbyStates.Add(state);

    /// <remarks>
    /// Insert-if-absent (<c>ON DUPLICATE KEY UPDATE</c> on <c>ux_lobby_states_learner</c>), then one tracked read: the
    /// Home lobby request and <c>assessment.attempt.submitted</c> often create the row at the same moment.
    /// Interpolated values are sent as parameters.
    /// </remarks>
    public async Task<LobbyState> GetOrCreateAsync(LobbyState candidate, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(candidate.LearnerId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var id = candidate.Id.ToString();
        var learnerId = candidate.LearnerId.ToString();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO lobby_states (id, tenant_id, learner_id, updated_at)
            VALUES ({id}, {candidate.TenantId}, {learnerId}, {candidate.UpdatedAt})
            ON DUPLICATE KEY UPDATE id = id
            """,
            cancellationToken);

        return await GetAsync(candidate.LearnerId, cancellationToken)
            ?? throw new InvalidOperationException("The lobby state row was not created.");
    }
}

internal sealed class ActivityRepository(LearningDbContext db) : IActivityRepository
{
    public Task<ActivityDay?> GetAsync(Guid learnerId, DateOnly day, CancellationToken cancellationToken) =>
        db.ActivityDays.FirstOrDefaultAsync(d => d.LearnerId == learnerId && d.Day == day, cancellationToken);

    public void Add(ActivityDay day) => db.ActivityDays.Add(day);
}

internal sealed class LearnerDataEraser(LearningDbContext db) : ILearnerDataEraser
{
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.BriefProgress.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.QuizAttempts.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.QuizDrafts.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.LobbyStates.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.ActivityDays.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
}
