using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Errors;
using SkillMuni.Common.Results;

namespace Learner.Application.Services;

/// <summary>API-057 / A-245: ESCO skill search. 503 lets the screen fall back to the offline decks.</summary>
public interface ISkillSearchService
{
    Task<Result<EscoSearchDto>> SearchAsync(string? text, string? language, int? limit, CancellationToken cancellationToken);
}

public sealed class SkillSearchService(IEscoClient esco) : ISkillSearchService
{
    public const int DefaultLimit = 24;
    public const int MaxLimit = 50;
    public const int MaxTextLength = 100;

    /// <summary>ESCO's languages that our markets use; others fall back to English.</summary>
    private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "en", "hi", "af", "zu", "xh" };

    public async Task<Result<EscoSearchDto>> SearchAsync(string? text, string? language, int? limit, CancellationToken cancellationToken)
    {
        var query = (text ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return new EscoSearchDto(0, []);
        }

        if (query.Length > MaxTextLength)
        {
            query = query[..MaxTextLength];
        }

        var lang = (language ?? "en").Split('-')[0].ToLowerInvariant();
        var found = await esco.SearchAsync(query, Languages.Contains(lang) ? lang : "en", Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit), cancellationToken);
        return found is { Status: LookupStatus.Found, Value: { } result } ? result : LearnerErrors.EscoUnavailable;
    }
}
