using SkillMuni.Common.Results;

namespace Platform.Application.Services;

/// <summary>API-032/033: address lookup by PIN / postal code, and the region list (Q2 Hit Me Up).</summary>
public interface IGeoService
{
    /// <summary>404 <c>postal-not-found</c> → the app shows "PIN not found" and lets the learner pick the state.</summary>
    Task<Result<PostalLookupDto>> LookupAsync(string market, string code, CancellationToken cancellationToken);

    /// <summary>An unknown market gets an empty list, as the mock does.</summary>
    Task<IReadOnlyList<string>> RegionsAsync(string market, CancellationToken cancellationToken);
}

public sealed class GeoService(IPostalDirectory directory) : IGeoService
{
    public async Task<Result<PostalLookupDto>> LookupAsync(string market, string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length is 0 or > 10)
        {
            return PlatformErrors.PostalNotFound;
        }

        var entry = await directory.FindAsync(NormalizeMarket(market), normalized, cancellationToken);
        if (entry is null)
        {
            return PlatformErrors.PostalNotFound;
        }

        return new PostalLookupDto(entry.Region, entry.District, entry.City, entry.Areas.Select(a => new PostalAreaDto(a, entry.District)).ToList());
    }

    public Task<IReadOnlyList<string>> RegionsAsync(string market, CancellationToken cancellationToken) =>
        directory.RegionsAsync(NormalizeMarket(market), cancellationToken);

    internal static string NormalizeMarket(string market) => market.Trim().ToUpperInvariant();

    internal static string NormalizeCode(string code) => new(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
