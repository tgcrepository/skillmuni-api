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
        var lore = Quest(JobProfileBuilder.WorkLore);
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
            NullIfEmpty(contact.Str("region")),
            Relocate(arc),
            Skills(flex, lore, receipts));
    }

    /// <summary>Pick Your Arc: whether "Open to relocating" is among the places (q6-arc.ts <c>where</c>); null when unanswered.</summary>
    private static bool? Relocate(QuestAnswerView arc) =>
        arc.Raw.ContainsKey("where") ? arc.Strings("where").Contains("relocate", StringComparer.OrdinalIgnoreCase) : null;

    /// <summary>
    /// The Quest 4 deck (skills.ts <c>DeckSkill</c>). A skill is "verified" when a verified proof is linked to it: a
    /// verified trophy (DigiLocker import or checked credential link, or a verified <c>tr-&lt;id&gt;</c> receipt) through
    /// its <c>linkedSkills</c>, or a work chapter with a verified <c>ch-&lt;id&gt;</c> receipt through its <c>skills</c>
    /// (both hold deck skill ids). Quest 7 receipts are keyed by claim (q7-receipts.ts).
    /// </summary>
    private static IReadOnlyList<CareersSkillDto> Skills(QuestAnswerView flex, QuestAnswerView lore, QuestAnswerView receipts)
    {
        var proofs = receipts.Map("receipts");
        bool ReceiptVerified(string key) =>
            proofs.TryGetValue(key, out var proof) && string.Equals(proof.Str("status"), Verified, StringComparison.OrdinalIgnoreCase);

        var proven = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trophy in flex.Items("trophies"))
        {
            var verified = trophy.Bool("verified") == true ||
                string.Equals(trophy.Str("source"), "digilocker", StringComparison.OrdinalIgnoreCase) ||
                ReceiptVerified($"tr-{trophy.Str("id")}");
            if (verified)
            {
                proven.UnionWith(trophy.Strings("linkedSkills"));
            }
        }

        foreach (var chapter in lore.Items("chapters").Where(c => ReceiptVerified($"ch-{c.Str("id")}")))
        {
            proven.UnionWith(chapter.Strings("skills"));
        }

        return flex.Items("skills")
            .Select(s => (Id: s.Str("id"), Name: s.Str("name")))
            .Where(s => s.Name.Length > 0)
            .DistinctBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new CareersSkillDto(s.Name, s.Id.Length > 0 && proven.Contains(s.Id) ? Verified : Declared))
            .ToList();
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
