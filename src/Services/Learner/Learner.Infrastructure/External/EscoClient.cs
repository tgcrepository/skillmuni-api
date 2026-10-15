using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Learner.Infrastructure.External;

/// <summary>
/// A-245: ESCO's public search (<c>GET https://ec.europa.eu/esco/api/search?type=skill&amp;text=…&amp;language=…</c>).
/// Keeps the concept URI and preferred label and maps each concept to one of the six deck categories from its
/// skill group code. Results are cached for a day (the taxonomy changes with ESCO releases, not daily).
/// </summary>
internal sealed partial class EscoClient(HttpClient http, IDistributedCache cache, ILogger<EscoClient> logger) : IEscoClient
{
    public const string BaseAddress = "https://ec.europa.eu/esco/api/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions CacheFor = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) };

    public async Task<Lookup<EscoSearchDto>> SearchAsync(string text, string language, int limit, CancellationToken cancellationToken)
    {
        var key = $"learner:esco:{language}:{limit.ToString(CultureInfo.InvariantCulture)}:{text.ToLowerInvariant()}";
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return Lookup<EscoSearchDto>.Found(JsonSerializer.Deserialize<EscoSearchDto>(cached, Json)!);
        }

        try
        {
            var path = $"search?type=skill&full=false&text={Uri.EscapeDataString(text)}&language={Uri.EscapeDataString(language)}&limit={limit.ToString(CultureInfo.InvariantCulture)}";
            using var document = await http.GetFromJsonAsync<JsonDocument>(path, cancellationToken);
            if (document is null)
            {
                return Lookup<EscoSearchDto>.Unavailable();
            }

            var result = Map(document.RootElement, language);
            await cache.SetStringAsync(key, JsonSerializer.Serialize(result, Json), CacheFor, cancellationToken);
            return Lookup<EscoSearchDto>.Found(result);
        }
        catch (Exception e) when (ExternalCall.Failed(e, cancellationToken))
        {
            LogUnavailable(logger, e);
            return Lookup<EscoSearchDto>.Unavailable();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ESCO search unavailable")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);

    /// <summary>Reads ESCO's HAL response defensively: missing fields fall back rather than fail the search.</summary>
    internal static EscoSearchDto Map(JsonElement root, string language)
    {
        var results = new List<EscoSkillDto>();
        if (root.TryGetProperty("_embedded", out var embedded) && embedded.TryGetProperty("results", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var uri = Text(item, "uri");
                if (uri is null)
                {
                    continue;
                }

                var label = Label(item, language) ?? Text(item, "title") ?? string.Empty;
                var groupCode = GroupCode(item);
                var isKnowledge = Uris(item, "hasSkillType").Any(t => t.EndsWith("/knowledge", StringComparison.Ordinal));
                var type = groupCode?.StartsWith('T') == true ? "transversal" : isKnowledge ? "knowledge" : "skill";
                results.Add(new EscoSkillDto(uri, label, type, Category(groupCode), EscoCategory: null, Alt: null));
            }
        }

        var total = root.TryGetProperty("total", out var t) && t.TryGetInt32(out var n) ? n : results.Count;
        return new EscoSearchDto(total, results);
    }

    /// <summary>
    /// ESCO skill-group code → deck category. S = skills pillar groups, K = knowledge (ISCED-F fields),
    /// T = transversal, L = language. Flagged to confirm with the content team (A-245).
    /// </summary>
    internal static string Category(string? code)
    {
        if (code is null)
        {
            return "business";
        }

        return code switch
        {
            _ when code.StartsWith("S1", StringComparison.Ordinal) => "people",
            _ when code.StartsWith("S2", StringComparison.Ordinal) => "data",
            _ when code.StartsWith("S3", StringComparison.Ordinal) => "people",
            _ when code.StartsWith("S4", StringComparison.Ordinal) => "business",
            _ when code.StartsWith("S5", StringComparison.Ordinal) => "tech",
            _ when code.StartsWith("S6", StringComparison.Ordinal) || code.StartsWith("S7", StringComparison.Ordinal) || code.StartsWith("S8", StringComparison.Ordinal) => "trades",
            _ when code.StartsWith("K06", StringComparison.Ordinal) => "tech",
            _ when code.StartsWith("K04", StringComparison.Ordinal) => "business",
            _ when code.StartsWith("K02", StringComparison.Ordinal) => "creative",
            _ when code.StartsWith("K05", StringComparison.Ordinal) => "data",
            _ when code.StartsWith("K07", StringComparison.Ordinal) || code.StartsWith("K08", StringComparison.Ordinal) => "trades",
            _ when code.StartsWith('K') || code.StartsWith('T') || code.StartsWith('L') => "people",
            _ => "business",
        };
    }

    /// <summary>The last segment of the first broader concept URI that looks like a group code (S4.2, K0613, T1…).</summary>
    private static string? GroupCode(JsonElement item) =>
        Uris(item, "broaderHierarchyConcept")
            .Select(u => u[(u.LastIndexOf('/') + 1)..])
            .FirstOrDefault(c => c.Length >= 2 && c[0] is 'S' or 'K' or 'T' or 'L' && char.IsAsciiDigit(c[1]));

    private static string? Label(JsonElement item, string language) =>
        item.TryGetProperty("preferredLabel", out var labels) && labels.ValueKind == JsonValueKind.Object
            ? Text(labels, language) ?? Text(labels, "en")
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<string> Uris(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()!],
            JsonValueKind.Array => value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : Text(v, "uri"))
                .OfType<string>()
                .ToList(),
            _ => [],
        };
    }
}
