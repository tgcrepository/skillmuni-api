using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Platform.Application;

namespace Platform.Infrastructure.Geo;

/// <summary>The starter dataset shipped inside the service (embedded <c>postal-starter.json</c>), read once.</summary>
internal sealed class EmbeddedPostalDirectory : IPostalDirectory
{
    public const string ResourceName = "Platform.Geo.postal-starter.json";

    private static readonly Lazy<Dataset> Data = new(Load);

    public Task<PostalEntry?> FindAsync(string market, string code, CancellationToken cancellationToken) =>
        Task.FromResult(Data.Value.Postal.GetValueOrDefault($"{market}/{code}"));

    public Task<IReadOnlyList<string>> RegionsAsync(string market, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Data.Value.Regions.GetValueOrDefault(market) ?? []);

    private static Dataset Load()
    {
        using var stream = typeof(EmbeddedPostalDirectory).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        var data = JsonSerializer.Deserialize<Dataset>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is empty.");
        return new Dataset(
            new Dictionary<string, PostalEntry>(data.Postal, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<string>>(data.Regions, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record Dataset(Dictionary<string, PostalEntry> Postal, Dictionary<string, List<string>> Regions);
}

/// <summary>
/// Postal answers cached in Redis (CLAUDE.md §8), misses included, so a real directory behind the interface is asked
/// once per code per <c>Platform:CacheFor</c>.
/// </summary>
internal sealed class CachedPostalDirectory(EmbeddedPostalDirectory inner, IDistributedCache cache, IOptions<PlatformOptions> options) : IPostalDirectory
{
    public async Task<PostalEntry?> FindAsync(string market, string code, CancellationToken cancellationToken)
    {
        var key = $"geo:postal:{market}:{code}";
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return cached.Length == 0 ? null : JsonSerializer.Deserialize<PostalEntry>(cached);
        }

        var entry = await inner.FindAsync(market, code, cancellationToken);
        await cache.SetStringAsync(key, entry is null ? string.Empty : JsonSerializer.Serialize(entry), Expiry(), cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyList<string>> RegionsAsync(string market, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"geo:regions:{market}");
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return JsonSerializer.Deserialize<List<string>>(cached) ?? [];
        }

        var regions = await inner.RegionsAsync(market, cancellationToken);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(regions), Expiry(), cancellationToken);
        return regions;
    }

    private DistributedCacheEntryOptions Expiry() => new() { AbsoluteExpirationRelativeToNow = options.Value.CacheFor };
}
