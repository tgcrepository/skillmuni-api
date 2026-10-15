using System.Globalization;

namespace Learning.Domain;

// Learning content: Module (/learn/:module, e.g. brain-vita) → Topic (e.g. non-verbal-reasoning) → Briefs, each brief
// followed by a quiz (BRD §3, learn.handlers.ts). Content is static seed data for now; a CMS can replace the provider.

/// <summary>The id of a brief and of its quiz: <c>{module}~{topic}~{index}</c> with a 0-based index (the mock's ids).</summary>
public readonly record struct BriefKey(string Module, string Topic, int Index)
{
    public const char Separator = '~';

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Module}{Separator}{Topic}{Separator}{Index}");

    public static bool TryParse(string? value, out BriefKey key)
    {
        key = default;
        var parts = value?.Split(Separator);
        if (parts is not [{ Length: > 0 } module, { Length: > 0 } topic, var index]
            || !int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var i))
        {
            return false;
        }

        key = new BriefKey(module, topic, i);
        return true;
    }
}

public sealed record BriefMedia(string Kind, string Src, string Alt, string? Duration = null, string? Poster = null);

public sealed record BriefContent(
    string Title,
    string Body,
    string? Example = null,
    string? KeyTerm = null,
    string? VideoUrl = null,
    BriefMedia? Media = null);

/// <summary>A quiz question. <c>Answer</c> is the index of the right option; <c>Notes</c> say why a wrong pick is wrong.</summary>
public sealed record QuizQuestion(
    string Id,
    string Prompt,
    IReadOnlyList<string> Options,
    int Answer,
    string Why,
    IReadOnlyDictionary<int, string>? Notes = null,
    string? Muni = null)
{
    public string OptionId(int index) => string.Create(CultureInfo.InvariantCulture, $"{Id}-o{index}");

    public string CorrectOptionId => OptionId(Answer);

    /// <summary>The option index inside an option id (<c>nv-q1-o2</c> → 2), or -1.</summary>
    public int IndexOf(string? optionId) =>
        optionId is not null && optionId.StartsWith(Id + "-o", StringComparison.Ordinal)
            && int.TryParse(optionId.AsSpan(Id.Length + 2), NumberStyles.None, CultureInfo.InvariantCulture, out var i)
            && i >= 0 && i < Options.Count
            ? i
            : -1;
}

public sealed record TopicContent(
    string Slug,
    string Title,
    string Art,
    IReadOnlyList<BriefContent> Briefs,
    IReadOnlyList<QuizQuestion> Questions,
    string? QuizTitle = null,
    string? Course = null)
{
    /// <summary>
    /// The questions of one brief's quiz. The design quiz belongs to the topic's last brief; earlier briefs get its first
    /// question only, unless the topic has a multi-question quiz (then every brief uses it). Same rule as the mock.
    /// </summary>
    public IReadOnlyList<QuizQuestion> QuestionsFor(int briefIndex) =>
        briefIndex == Briefs.Count - 1 || Questions.Count > 1 ? Questions : Questions.Take(1).ToList();

    /// <summary>The most XP the topic's quizzes can pay (+1 per question on each first submit).</summary>
    public int MaxXp => Enumerable.Range(0, Briefs.Count).Sum(i => QuestionsFor(i).Count);
}

/// <summary>A module and the Lobby Knook it sits in. <c>TileArt</c> is its Lobby tile, <c>Tone</c> its My Stats colour.</summary>
public sealed record ModuleContent(
    string Slug,
    string Title,
    string Tagline,
    KnookRef Knook,
    string TileArt,
    string Tone,
    IReadOnlyList<TopicContent> Topics)
{
    public int BriefCount => Topics.Sum(t => t.Briefs.Count);

    public TopicContent? Topic(string slug) => Topics.FirstOrDefault(t => t.Slug == slug);
}

public sealed record KnookRef(string Slug, string Title);

/// <summary>A Lobby pathway card (Career Launchpad, Grad Gateway, Startup Street).</summary>
public sealed record Pathway(string Slug, string Title, string Blurb, string Tone, string Art, string? Link);

/// <summary>A Lobby shelf of module tiles.</summary>
public sealed record Knook(string Slug, string Title, string Icon, bool Expanded, IReadOnlyList<KnookTile> Modules);

public sealed record KnookTile(string Slug, string Title, string Art, string Link);
