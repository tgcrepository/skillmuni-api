using Learner.Application.Abstractions;
using Learner.Domain;
using SkillMuni.Common.Results;

namespace Learner.Application.Services;

/// <summary>One skill as Careers matches it: <c>verified</c> when a proof for it is verified, else <c>declared</c>.</summary>
public sealed record CareersSkillDto(string Name, string Evidence);

/// <summary>
/// What Careers needs to match and rank a learner (internal API). Contact is masked; nothing here is shown to
/// employers as is.
/// </summary>
public sealed record CareersProfileDto(
    string? Name,
    string? Contact,
    string? City,
    string? State,
    bool? Relocate,
    IReadOnlyList<CareersSkillDto> Skills);

/// <summary><c>GET internal/learners/{id}/careers-profile</c>: read from the quest answers, 404 without a learner.</summary>
public interface ICareersProfileService
{
    Task<Result<CareersProfileDto>> GetAsync(Guid learnerId, CancellationToken cancellationToken);
}

public sealed class CareersProfileService(ILearnerRepository learners, IQuestAnswersRepository answers) : ICareersProfileService
{
    public const string Verified = "verified";
    public const string Declared = "declared";

    private static readonly Error LearnerNotFound = Error.NotFound("learner-not-found", "Learner not found");

    public async Task<Result<CareersProfileDto>> GetAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var learner = await learners.GetAsync(learnerId, cancellationToken);
        if (learner is null)
        {
            return LearnerNotFound;
        }

        return Build(learner, await answers.ListAsync(learnerId, cancellationToken));
    }

    internal static CareersProfileDto Build(LearnerProfile learner, IReadOnlyDictionary<string, string> answersBySlug)
    {
        QuestAnswerView Quest(string slug) => QuestAnswerView.Parse(answersBySlug.GetValueOrDefault(slug));
        var whoDis = Quest(JobProfileBuilder.WhoDis);
        var contact = Quest(JobProfileBuilder.HitMeUp);
        var flex = Quest(JobProfileBuilder.Flex);
        var arc = Quest(JobProfileBuilder.Arc);
        var receipts = Quest(JobProfileBuilder.Receipts);

        // Quest 1's name, falling back to the learner's own (both kept in step by the quest save).
        var name = string.Join(' ', new[] { whoDis.Str("firstName"), whoDis.Str("lastName") }.Where(n => n.Length > 0));
        if (name.Length == 0)
        {
            name = learner.FullName;
        }

        // The verified mobile copy first, then what Quest 2 holds; masked like the Profile (never the full number).
        var phone = learner.Phone ?? NullIfEmpty(contact.Str("phone"));
        var masked = ProfileFormat.MaskMobile(phone);

        return new CareersProfileDto(
            NullIfEmpty(name),
            masked == ProfileFormat.NotAdded ? null : masked,
            NullIfEmpty(contact.Str("city")),
            NullIfEmpty(contact.Str("region", "state")),
            Relocate(arc),
            Skills(flex, receipts));
    }

    /// <summary>Pick Your Arc: an explicit <c>relocate</c> flag, else whether "relocate" is among the places; null when unanswered.</summary>
    private static bool? Relocate(QuestAnswerView arc)
    {
        if (arc.Bool("relocate") is { } flag)
        {
            return flag;
        }

        if (!arc.Raw.ContainsKey("where"))
        {
            return null;
        }

        return arc.Strings("where").Contains("relocate", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Quest 4 deck. A skill is "verified" when its own entry is verified, or a verified proof names it: a trophy
    /// (DigiLocker import or checked certificate) or a Quest 7 receipt whose <c>skills</c>/<c>skill</c> lists it.
    /// </summary>
    private static IReadOnlyList<CareersSkillDto> Skills(QuestAnswerView flex, QuestAnswerView receipts)
    {
        var proven = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proof in flex.Items("trophies").Concat(receipts.Items("receipts")).Where(IsVerified))
        {
            proven.UnionWith(proof.Strings("skills"));
            if (proof.Str("skill") is { Length: > 0 } one)
            {
                proven.Add(one);
            }
        }

        return flex.Items("skills")
            .Select(s => (Id: s.Str("id"), Name: s.Str("name"), Verified: IsVerified(s)))
            .Where(s => s.Name.Length > 0)
            .DistinctBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new CareersSkillDto(
                s.Name,
                s.Verified || proven.Contains(s.Name) || (s.Id.Length > 0 && proven.Contains(s.Id)) ? Verified : Declared))
            .ToList();
    }

    private static bool IsVerified(QuestAnswerView item) =>
        item.Bool("verified") == true ||
        string.Equals(item.Str("source"), "digilocker", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.Str("proof", "status"), Verified, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
