using System.Text.Json;
using Cv.Application;
using Cv.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cv.Infrastructure.Persistence;

internal sealed class CvRepository(CvDbContext db) : ICvRepository
{
    public Task<CvDocument?> FindAsync(Guid cvId, Guid learnerId, CancellationToken cancellationToken) =>
        db.Documents.FirstOrDefaultAsync(d => d.Id == cvId && d.LearnerId == learnerId, cancellationToken);

    public Task<CvDocument?> GetAsync(Guid cvId, CancellationToken cancellationToken) =>
        db.Documents.FirstOrDefaultAsync(d => d.Id == cvId, cancellationToken);

    public Task<int> CountUploadsSinceAsync(Guid learnerId, DateTime since, CancellationToken cancellationToken) =>
        db.Documents.AsNoTracking().CountAsync(d => d.LearnerId == learnerId && d.CreatedAt >= since, cancellationToken);

    public async Task<IReadOnlyList<CvDocument>> ListForLearnerAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.Documents.Where(d => d.LearnerId == learnerId).ToListAsync(cancellationToken);

    public void Add(CvDocument document) => db.Documents.Add(document);

    public void Remove(CvDocument document) => db.Documents.Remove(document);
}

/// <summary>
/// The parse queue is the <c>cv_documents</c> table itself (index on state + created_at). A claim is a conditional
/// UPDATE, so two replicas never take the same job. The worker spans tenants, so these queries skip the tenant filter.
/// </summary>
internal sealed class CvJobQueue(CvDbContext db) : ICvJobQueue
{
    private const int Candidates = 5;

    public async Task<ClaimedJob?> ClaimNextAsync(DateTime now, DateTime staleBefore, int maxAttempts, CancellationToken cancellationToken)
    {
        // Jobs whose worker died too often are failed instead of retried forever.
        await db.Documents.IgnoreQueryFilters()
            .Where(d => d.State == ParseState.Parsing && d.ClaimedAt < staleBefore && d.Attempts >= maxAttempts)
            .ExecuteUpdateAsync(
                u => u.SetProperty(d => d.State, ParseState.Failed)
                    .SetProperty(d => d.Outcome, "parse-failed")
                    .SetProperty(d => d.CompletedAt, now)
                    .SetProperty(d => d.UpdatedAt, now),
                cancellationToken);

        var candidates = await db.Documents.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.State == ParseState.Queued || (d.State == ParseState.Parsing && d.ClaimedAt < staleBefore))
            .OrderBy(d => d.CreatedAt)
            .Select(d => new { d.Id, d.TenantId, d.State, d.ClaimedAt })
            .Take(Candidates)
            .ToListAsync(cancellationToken);

        foreach (var c in candidates)
        {
            var claimed = await db.Documents.IgnoreQueryFilters()
                .Where(d => d.Id == c.Id && d.State == c.State && d.ClaimedAt == c.ClaimedAt)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(d => d.State, ParseState.Parsing)
                        .SetProperty(d => d.Step, 0)
                        .SetProperty(d => d.ClaimedAt, now)
                        .SetProperty(d => d.Attempts, d => d.Attempts + 1)
                        .SetProperty(d => d.UpdatedAt, now),
                    cancellationToken);
            if (claimed == 1)
            {
                return new ClaimedJob(c.Id, c.TenantId);
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<ExpiredFiles>> ListExpiredFilesAsync(DateTime before, int max, CancellationToken cancellationToken)
    {
        var rows = await db.Documents.IgnoreQueryFilters().AsNoTracking()
            .Where(d => (d.State == ParseState.Done || d.State == ParseState.Failed) && d.CompletedAt < before && d.BlobNamesJson != "[]")
            .OrderBy(d => d.CompletedAt)
            .Select(d => new { d.Id, d.BlobNamesJson })
            .Take(max)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new ExpiredFiles(r.Id, JsonSerializer.Deserialize<List<string>>(r.BlobNamesJson) ?? [])).ToList();
    }

    public Task ForgetFilesAsync(Guid cvId, CancellationToken cancellationToken) =>
        db.Documents.IgnoreQueryFilters()
            .Where(d => d.Id == cvId)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.BlobNamesJson, "[]"), cancellationToken);
}
