using System.Text.Json;
using System.Text.RegularExpressions;
using Learner.Application.Abstractions;
using Learner.Application.Errors;
using Learner.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-034/035: the offline queue's autosave. Any JSON value under a client-chosen key, per learner.</summary>
public interface IDraftService
{
    Task<Result> SaveAsync(Guid learnerId, string key, JsonElement value, CancellationToken cancellationToken);

    Task<Result<JsonElement>> GetAsync(Guid learnerId, string key, CancellationToken cancellationToken);
}

public sealed partial class DraftService(
    IDraftRepository drafts,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options) : IDraftService
{
    private static readonly Error InvalidKey = Error.Validation(
        "invalid-key",
        "Draft keys are 1–100 letters, digits or . _ : -",
        new Dictionary<string, string[]> { ["key"] = ["Draft keys are 1–100 letters, digits or . _ : -"] });

    public async Task<Result> SaveAsync(Guid learnerId, string key, JsonElement value, CancellationToken cancellationToken)
    {
        if (!KeyPattern().IsMatch(key))
        {
            return InvalidKey;
        }

        var json = value.GetRawText();
        if (AnswersJson.Utf8Length(json) > options.Value.MaxDraftBytes)
        {
            return LearnerErrors.DraftTooLarge;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var draft = await drafts.FindAsync(learnerId, key, cancellationToken);
        if (draft is null)
        {
            draft = Draft.Create(tenant.TenantId, learnerId, key, now);
            drafts.Add(draft);
        }

        draft.Save(json, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<JsonElement>> GetAsync(Guid learnerId, string key, CancellationToken cancellationToken)
    {
        if (!KeyPattern().IsMatch(key))
        {
            return InvalidKey;
        }

        var draft = await drafts.FindAsync(learnerId, key, cancellationToken);
        return draft is null ? LearnerErrors.DraftNotFound : AnswersJson.ToElement(draft.ValueJson);
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
