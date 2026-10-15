using System.Globalization;
using System.Text.Json;
using Gamification.Application;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Tenancy;

namespace Gamification.Infrastructure.Persistence;

/// <summary>
/// The "everyone" boards are the same for every learner in a tenant, so they are cached briefly (Redis when
/// configured, CLAUDE.md §8). Personal boards (batch, friends) and "yesterday" boards go straight to the database.
/// </summary>
internal sealed class CachedBoardQueries(BoardQueries inner, IDistributedCache cache, ITenantContext tenant, IOptions<GamificationOptions> options) : IBoardQueries
{
    public async Task<IReadOnlyList<BoardRow>> TopAsync(DateTime? since, DateTime? before, IReadOnlyCollection<Guid>? only, int limit, CancellationToken cancellationToken)
    {
        if (only is not null || before is not null)
        {
            return await inner.TopAsync(since, before, only, limit, cancellationToken);
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"gamification:board:{tenant.TenantId}:{since?.Ticks ?? 0}:{limit}");
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return JsonSerializer.Deserialize<List<BoardRow>>(cached) ?? [];
        }

        var rows = await inner.TopAsync(since, before, only, limit, cancellationToken);
        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(rows),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.Value.Arena.CacheFor },
            cancellationToken);
        return rows;
    }

    public Task<(BoardRow Row, int Rank)?> RankOfAsync(Guid learnerId, DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken) =>
        inner.RankOfAsync(learnerId, since, only, cancellationToken);

    public Task<int> CountAsync(DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken) =>
        inner.CountAsync(since, only, cancellationToken);
}
