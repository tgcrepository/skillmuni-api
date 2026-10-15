using Learning.Application.Contracts;
using Learning.Domain;

namespace Learning.Application.Services;

/// <summary>
/// A learner's progress through one module, computed from their passed briefs. A brief is <c>done</c> once its quiz was
/// submitted; the first brief not done is <c>current</c>; later ones are <c>locked</c> (Figma 79:3005, the mock's rule).
/// Rows for briefs the catalogue no longer has are ignored.
/// </summary>
public sealed class ModuleProgress
{
    private readonly Dictionary<(string Topic, int Index), PassedBrief> _passed;

    public ModuleProgress(ModuleContent module, IEnumerable<PassedBrief> passed)
    {
        Module = module;
        _passed = passed
            .Where(p => p.ModuleSlug == module.Slug && module.Topic(p.TopicSlug) is { } t && p.BriefIndex >= 0 && p.BriefIndex < t.Briefs.Count)
            .GroupBy(p => (p.TopicSlug, p.BriefIndex))
            .ToDictionary(g => g.Key, g => g.First());
    }

    public ModuleContent Module { get; }

    public int QuizzesDone => _passed.Count;

    public int QuizzesTotal => Module.BriefCount;

    public int Xp => _passed.Values.Sum(p => p.Xp);

    public bool Completed => QuizzesDone >= QuizzesTotal;

    public static ModuleProgress From(ModuleContent module, IEnumerable<BriefProgress> rows) =>
        new(module, rows.Where(r => r.Passed).Select(ToPassed));

    public static PassedBrief ToPassed(BriefProgress row) =>
        new(row.ModuleSlug, row.TopicSlug, row.BriefIndex, row.Xp, row.LastSubmittedAt ?? row.PassedAt ?? row.UpdatedAt);

    public bool IsPassed(TopicContent topic, int index) => _passed.ContainsKey((topic.Slug, index));

    public int XpOf(TopicContent topic, int index) => _passed.TryGetValue((topic.Slug, index), out var p) ? p.Xp : 0;

    public int Done(TopicContent topic) => Enumerable.Range(0, topic.Briefs.Count).Count(i => IsPassed(topic, i));

    public int TopicXp(TopicContent topic) => Enumerable.Range(0, topic.Briefs.Count).Sum(i => XpOf(topic, i));

    public bool IsDone(TopicContent topic) => Done(topic) >= topic.Briefs.Count;

    /// <summary>The first brief not done (the "current" one), or the brief count when the topic is done.</summary>
    public int Current(TopicContent topic)
    {
        for (var i = 0; i < topic.Briefs.Count; i++)
        {
            if (!IsPassed(topic, i))
            {
                return i;
            }
        }

        return topic.Briefs.Count;
    }

    public string StatusOf(TopicContent topic, int index) =>
        IsPassed(topic, index) ? "done" : index == Current(topic) ? "current" : "locked";

    /// <summary>The brief whose quiz is up next, or the last brief once every brief is done (practice).</summary>
    public int QuizBrief(TopicContent topic) => Math.Min(Current(topic), topic.Briefs.Count - 1);

    /// <summary>
    /// Where the learner left off (B-44): the current brief of <paramref name="preferTopic"/> if it isn't done, else the
    /// current brief of the first unfinished topic in catalogue order. Null when the whole module is done.
    /// </summary>
    public (TopicContent Topic, int Index)? Resume(string? preferTopic)
    {
        if (preferTopic is not null && Module.Topic(preferTopic) is { } preferred && !IsDone(preferred))
        {
            return (preferred, Current(preferred));
        }

        var next = Module.Topics.FirstOrDefault(t => !IsDone(t));
        return next is null ? null : (next, Current(next));
    }

    /// <summary>The topic of the brief the learner checked most recently in this module, if any.</summary>
    public string? LastTopic() => _passed.Values.OrderByDescending(p => p.LastSubmittedAt).FirstOrDefault()?.TopicSlug;

    public TopicSummaryDto Summary(TopicContent topic) =>
        new(topic.Slug, topic.Title, topic.Art, Done(topic), topic.Briefs.Count, TopicXp(topic));

    public ModuleResumeDto? ResumeDto()
    {
        var resume = Resume(LastTopic());
        if (resume is null)
        {
            return null;
        }

        var (topic, index) = resume.Value;
        var key = new BriefKey(Module.Slug, topic.Slug, index);
        return new ModuleResumeDto(
            new SlugTitleDto(topic.Slug, topic.Title),
            new ResumeBriefDto(key.ToString(), index + 1, topic.Briefs[index].Title),
            Links.Topic(Module.Slug, topic.Slug));
    }
}

/// <summary>App routes the API hands out.</summary>
public static class Links
{
    public static string Module(string module) => $"/learn/{module}";

    public static string Topic(string module, string topic) => $"/learn/{module}/{topic}";

    public static string Course(string module, string topic) => $"/learn/{module}/{topic}/course";
}
