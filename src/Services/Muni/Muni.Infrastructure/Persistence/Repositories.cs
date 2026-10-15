using Microsoft.EntityFrameworkCore;
using Muni.Application;
using Muni.Domain;

namespace Muni.Infrastructure.Persistence;

internal sealed class MuniLearnerRepository(MuniDbContext db) : IMuniLearnerRepository
{
    public Task<MuniLearner?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Learners.FirstOrDefaultAsync(l => l.Id == learnerId, cancellationToken);

    public Task<MuniLearner?> FindAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Learners.AsNoTracking().FirstOrDefaultAsync(l => l.Id == learnerId, cancellationToken);

    public void Add(MuniLearner learner) => db.Learners.Add(learner);
}

internal sealed class ChatRepository(MuniDbContext db) : IChatRepository
{
    public Task<ChatLine?> LastAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Messages.AsNoTracking()
            .Where(m => m.LearnerId == learnerId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new ChatLine(m.Id, m.ConversationId, m.From, m.Text, m.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatLine>> RecentAsync(Guid learnerId, DateTime since, int limit, CancellationToken cancellationToken) =>
        await db.Messages.AsNoTracking()
            .Where(m => m.LearnerId == learnerId && m.CreatedAt >= since)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new ChatLine(m.Id, m.ConversationId, m.From, m.Text, m.CreatedAt))
            .ToListAsync(cancellationToken);

    public void Add(ChatMessage message) => db.Messages.Add(message);
}

internal sealed class RatingRepository(MuniDbContext db) : IRatingRepository
{
    public Task<ReplyRating?> FindAsync(Guid learnerId, string messageRef, CancellationToken cancellationToken) =>
        db.Ratings.FirstOrDefaultAsync(r => r.LearnerId == learnerId && r.MessageRef == messageRef, cancellationToken);

    public void Add(ReplyRating rating) => db.Ratings.Add(rating);

    public void Remove(ReplyRating rating) => db.Ratings.Remove(rating);
}

/// <summary>MISS-08. Set-based deletes in one transaction (inside the retrying execution strategy).</summary>
internal sealed class MuniDataEraser(MuniDbContext db) : IMuniDataEraser
{
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Messages.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Ratings.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Learners.Where(x => x.Id == learnerId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
}

/// <summary>Deletes old lines across every tenant (the worker has no tenant), oldest first, in batches.</summary>
internal sealed class ChatRetention(MuniDbContext db) : IChatRetention
{
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, int batch, CancellationToken cancellationToken)
    {
        var oldMessages = await db.Messages.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.CreatedAt < cutoff)
            .OrderBy(m => m.CreatedAt)
            .Take(batch)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var deleted = oldMessages.Count == 0
            ? 0
            : await db.Messages.IgnoreQueryFilters().Where(m => oldMessages.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);

        var oldRatings = await db.Ratings.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.RatedAt < cutoff)
            .OrderBy(r => r.RatedAt)
            .Take(batch)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (oldRatings.Count > 0)
        {
            deleted += await db.Ratings.IgnoreQueryFilters().Where(r => oldRatings.Contains(r.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        return deleted;
    }
}
