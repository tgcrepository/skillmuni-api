using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Common.Privacy;

namespace SkillMuni.Messaging.Erasure;

/// <summary>Tombstones in <typeparamref name="TContext"/>'s <c>erased_learners</c> table.</summary>
public sealed class EfErasureTombstones<TContext>(TContext db, ErasedLearnerCache cache, TimeProvider clock) : IErasureTombstones
    where TContext : DbContext
{
    public async Task<bool> IsErasedAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        if (cache.Contains(learnerId) || db.Set<ErasedLearner>().Local.Any(e => e.LearnerId == learnerId))
        {
            return true;
        }

        var erased = await db.Set<ErasedLearner>().AsNoTracking().AnyAsync(e => e.LearnerId == learnerId, cancellationToken);
        if (erased)
        {
            cache.Add(learnerId);
        }

        return erased;
    }

    public async Task MarkErasedAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        if (db.Set<ErasedLearner>().Local.Any(e => e.LearnerId == learnerId)
            || await db.Set<ErasedLearner>().AsNoTracking().AnyAsync(e => e.LearnerId == learnerId, cancellationToken))
        {
            return;
        }

        db.Set<ErasedLearner>().Add(new ErasedLearner(learnerId, clock.GetUtcNow().UtcDateTime));
    }
}

/// <summary>
/// Per-replica memory of ids already seen erased, so a busy erased token doesn't cost a query per request. Only
/// "erased" is remembered (never "not erased"), so the cache can't let a request through after erasure.
/// </summary>
public sealed class ErasedLearnerCache
{
    private const int MaxEntries = 10_000;

    private readonly ConcurrentDictionary<Guid, byte> _ids = new();

    public bool Contains(Guid learnerId) => _ids.ContainsKey(learnerId);

    public void Add(Guid learnerId)
    {
        // Access tokens live 10 minutes, so old entries are worth little; a full cache just starts again.
        if (_ids.Count >= MaxEntries)
        {
            _ids.Clear();
        }

        _ids.TryAdd(learnerId, 0);
    }
}
