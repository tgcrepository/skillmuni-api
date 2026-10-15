using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Domain;
using SkillMuni.Common.Results;

namespace Learner.Application.Services;

/// <summary>
/// A-262: the job profile is built from the 8 quests, so completeness is counted step by step. A step is "left"
/// when its quest isn't reached yet, or when the learner skipped it (optional steps store <c>&lt;key&gt;Skipped</c>).
/// Served on the internal API for CV, Careers (Launchpad, My Jobs) and Identity's claim result.
/// </summary>
public interface IProfileProgressService
{
    Task<Result<ProfileProgressDto>> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    ProfileProgressDto Calculate(LearnerProfile learner, IReadOnlyDictionary<string, string> answersBySlug);
}

public sealed class ProfileProgressService(
    ILearnerRepository learners,
    IQuestAnswersRepository answers,
    QuestCatalog catalog) : IProfileProgressService
{
    private static readonly Error LearnerNotFound = Error.NotFound("learner-not-found", "Learner not found");

    public async Task<Result<ProfileProgressDto>> GetAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var learner = await learners.GetAsync(learnerId, cancellationToken);
        if (learner is null)
        {
            return LearnerNotFound;
        }

        return Calculate(learner, await answers.ListAsync(learnerId, cancellationToken));
    }

    public ProfileProgressDto Calculate(LearnerProfile learner, IReadOnlyDictionary<string, string> answersBySlug)
    {
        var quests = catalog.Quests.Select(quest =>
        {
            var json = answersBySlug.GetValueOrDefault(quest.Slug, AnswersJson.Empty);
            var steps = quest.Parts.Select((p, i) => new QuestStepDto(p.Key, CleanLabel(p.Label), i + 1)).ToList();
            var left = steps.Where(step =>
            {
                if (quest.Number > learner.QuestsCompleted)
                {
                    // The quest in progress: steps after the last one saved, or skipped. Later quests: everything.
                    return quest.Number != learner.QuestsCompleted + 1 || step.Part > learner.PartsDone || AnswersJson.IsSkipped(json, step.Key);
                }

                return AnswersJson.IsSkipped(json, step.Key);
            }).ToList();
            return new QuestFillDto(quest.Number, quest.Slug, quest.Name, steps.Count - left.Count, steps.Count, left);
        }).ToList();

        var total = quests.Sum(q => q.Total);
        var filled = quests.Sum(q => q.Filled);
        var percent = total == 0 ? 0 : (int)Math.Round(filled * 100.0 / total, MidpointRounding.AwayFromZero);
        return new ProfileProgressDto(percent, filled, total, quests);
    }

    private static string CleanLabel(string label)
    {
        foreach (var suffix in new[] { "· optional", "(optional)" })
        {
            var at = label.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (at > 0 && at + suffix.Length == label.Length)
            {
                return label[..at].Trim();
            }
        }

        return label.Trim();
    }
}
