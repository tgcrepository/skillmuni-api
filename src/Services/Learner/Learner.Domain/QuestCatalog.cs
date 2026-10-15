namespace Learner.Domain;

public sealed record QuestPart(string Key, string Label, bool Optional);

public sealed record Quest(int Number, string Slug, string Name, string Badge, IReadOnlyList<QuestPart> Parts);

/// <summary>The 8 registration quests, in order. Loaded from configuration so it can track the frontend's quest definitions.</summary>
public sealed class QuestCatalog
{
    private readonly Dictionary<string, Quest> _bySlug;

    public QuestCatalog(IEnumerable<Quest> quests)
    {
        Quests = quests.OrderBy(q => q.Number).ToList();
        _bySlug = Quests.ToDictionary(q => q.Slug, StringComparer.Ordinal);
    }

    public IReadOnlyList<Quest> Quests { get; }

    public Quest? Find(string slug) => _bySlug.GetValueOrDefault(slug);
}
