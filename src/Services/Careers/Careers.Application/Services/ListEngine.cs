using System.Text.RegularExpressions;
using Careers.Application.Catalogue;
using Careers.Application.Contracts;

namespace Careers.Application.Services;

/// <summary>An item's values for one filter key; null = the key doesn't apply to this list (ignored).</summary>
public delegate IReadOnlyList<string>? FacetGetter<in T>(T item, string key);

/// <summary>
/// The My Jobs faceted search (FR-39, jobs.handlers.ts): AND across keys and OR within one; City/State and
/// "include remote" act together; every option counts what it would return with the other filters applied; zero
/// results suggest the filters to drop (J6).
/// </summary>
public static partial class ListEngine
{
    public const string RemoteKey = "remote";

    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new(StringComparer.Ordinal)
    {
        ["workMode"] = new() { ["onsite"] = "On-site", ["hybrid"] = "Hybrid", ["remote"] = "Remote" },
        ["jobType"] = new() { ["full-time"] = "Full-time", ["learnership"] = "Learnership / Internship", ["part-time"] = "Part-time", ["contract"] = "Contract" },
        ["pay"] = new() { ["1"] = "₹1 LPA+", ["2"] = "₹2 LPA+", ["3"] = "₹3 LPA+", ["5"] = "₹5 LPA+" },
        ["posted"] = new() { ["1"] = "Past 24 hours", ["7"] = "Past week", ["30"] = "Past month" },
        ["match"] = new() { ["90"] = "90%+ match", ["80"] = "80%+ match", ["70"] = "70%+ match", ["60"] = "60%+ match" },
        ["jobStatus"] = new() { ["open"] = "Open", ["closing"] = "Closing soon", ["closed"] = "Closed" },
        ["remote"] = new() { ["yes"] = "Include remote" },
        ["status"] = new()
        {
            ["new"] = "New",
            ["accepted"] = "Accepted",
            ["declined"] = "Declined",
            ["expired"] = "Expired",
            ["applied"] = "Applied",
            ["viewed"] = "Under review",
            ["shortlisted"] = "Shortlisted",
            ["interview"] = "Interview",
            ["offer"] = "Offer",
            ["not-selected"] = "Not selected",
            ["withdrawn"] = "Withdrawn",
        },
        ["inviteType"] = new() { ["apply"] = "Invited to apply", ["interview"] = "Interview invite" },
        ["dateApplied"] = new() { ["7"] = "Past week", ["30"] = "Past month", ["90"] = "Past 3 months" },
    };

    public static string Label(string key, string id) =>
        Labels.TryGetValue(key, out var byId) && byId.TryGetValue(id, out var label) ? label : id;

    public static bool Passes<T>(T item, IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> filters, FacetGetter<T> get, string? skip = null)
    {
        var includeRemote = filters.Any(f => f.Key == RemoteKey && f.Value.Contains("yes"));
        foreach (var (key, want) in filters)
        {
            if (want.Count == 0 || key == skip || key == RemoteKey)
            {
                continue;
            }

            var have = get(item, key);
            if (have is null)
            {
                continue;
            }

            if ((key == "city" || key == "state") && includeRemote && (get(item, "workMode") ?? []).Contains("remote"))
            {
                continue;
            }

            if (!want.Any(w => have.Contains(w)))
            {
                return false;
            }
        }

        return true;
    }

    public static FacetDto Facet<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> filters,
        FacetGetter<T> get,
        string key,
        IEnumerable<string> ids,
        Func<string, string>? label = null)
    {
        var pool = items.Where(i => Passes(i, filters, get, key)).ToList();
        return new FacetDto(
            key,
            ids.Select(id => new FacetOptionDto(id, label?.Invoke(id) ?? Label(key, id), pool.Count(i => (get(i, key) ?? []).Contains(id)))).ToList());
    }

    /// <summary>J6: for each chosen option, how many results dropping it would bring back; the 3 smallest wins.</summary>
    public static IReadOnlyList<SuggestionDto> Suggestions<T>(IReadOnlyList<T> items, IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> filters, FacetGetter<T> get)
    {
        var found = new List<SuggestionDto>();
        foreach (var (key, values) in filters)
        {
            foreach (var id in values)
            {
                var next = filters
                    .Select(f => f.Key == key ? new KeyValuePair<string, IReadOnlyList<string>>(key, values.Where(v => v != id).ToList()) : f)
                    .ToList();
                var count = items.Count(i => Passes(i, next, get));
                if (count > 0)
                {
                    found.Add(new SuggestionDto(key, id, Label(key, id), count));
                }
            }
        }

        return found.OrderBy(s => s.Count).Take(3).ToList();
    }

    /// <summary>The search box (title, company, skills, role, sector) and the place box ("Pune and Remote", "Pune, Mumbai").</summary>
    public static List<T> Text<T>(IEnumerable<T> items, string q, string place, Func<T, CatalogueJob> job)
    {
        var query = q.Trim().ToLowerInvariant();
        var places = place.Trim().ToLowerInvariant() is { Length: > 0 } p ? PlaceSplit().Split(p).Select(x => x.Trim()).ToList() : null;
        return items.Where(t =>
        {
            var j = job(t);
            var hay = $"{j.Title} {j.Company} {string.Join(' ', j.Skills)} {j.Role} {j.Sector}".ToLowerInvariant();
            var where = $"{j.City} {j.State} {(j.Remote ? "remote" : string.Empty)}".ToLowerInvariant();
            return (query.Length == 0 || hay.Contains(query, StringComparison.Ordinal))
                && (places is null || places.Any(x => where.Contains(x, StringComparison.Ordinal)));
        }).ToList();
    }

    public static ListResultDto<T> List<T>(IReadOnlyList<T> hits, int limit, IReadOnlyList<FacetDto> facets, IReadOnlyList<SuggestionDto> suggestions, string? note) =>
        new(hits.Take(limit).ToList(), hits.Count, facets, hits.Count == 0 ? suggestions : [], note);

    public static string Plural(int count, string word) => count == 1 ? $"{count} {word}" : $"{count} {word}s";

    /// <summary>
    /// The query string jobs.api.ts sends. <c>filters</c> is JSON (<c>{"sector":["Retail"]}</c>), read in key order;
    /// unreadable filters are ignored like the mock does. <c>limit</c> defaults to 20.
    /// </summary>
    public static ListQuery ParseQuery(string? q, string? place, string? sort, int? limit, string? filters, int maxLimit)
    {
        var parsed = new List<KeyValuePair<string, IReadOnlyList<string>>>();
        if (!string.IsNullOrWhiteSpace(filters))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(filters);
                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        List<string> values = property.Value.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.Array => property.Value.EnumerateArray()
                                .Where(v => v.ValueKind == System.Text.Json.JsonValueKind.String)
                                .Select(v => v.GetString()!)
                                .ToList(),
                            System.Text.Json.JsonValueKind.String => [property.Value.GetString()!],
                            _ => [],
                        };
                        parsed.RemoveAll(p => p.Key == property.Name);
                        parsed.Add(new(property.Name, values));
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                parsed.Clear();
            }
        }

        return new ListQuery(q ?? string.Empty, place ?? string.Empty, sort ?? string.Empty, Math.Clamp(limit ?? 20, 0, maxLimit), parsed);
    }

    [GeneratedRegex(@"\s+and\s+|,\s*")]
    private static partial Regex PlaceSplit();
}
