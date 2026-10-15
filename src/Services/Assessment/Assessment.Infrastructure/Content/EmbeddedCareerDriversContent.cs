using System.Text.Json;
using Assessment.Application;
using Assessment.Domain;

namespace Assessment.Infrastructure.Content;

/// <summary>
/// The survey content shipped inside the service as an embedded JSON resource (verbatim from the client document, via
/// the frontend's <c>assessment.data.ts</c>). Loaded and checked once; a CMS-backed provider can replace it.
/// </summary>
internal sealed class EmbeddedCareerDriversContent : ICareerDriversContentProvider
{
    public const string ResourceName = "Assessment.Content.career-drivers.json";

    private static readonly Lazy<CareerDriversContent> Loaded = new(Load);

    public CareerDriversContent Content => Loaded.Value;

    internal static CareerDriversContent Load()
    {
        using var stream = typeof(EmbeddedCareerDriversContent).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        var content = JsonSerializer.Deserialize<CareerDriversContent>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Career Drivers content is empty.");
        Validate(content);
        return content;
    }

    /// <summary>Fails start-up rather than scoring against broken content.</summary>
    internal static void Validate(CareerDriversContent content)
    {
        var problems = new List<string>();
        if (content.Pairs.Count == 0)
        {
            problems.Add("no pairs");
        }

        problems.AddRange(content.Pairs
            .Where(p => !Drivers.IsDriver(p.This.Driver) || !Drivers.IsDriver(p.That.Driver))
            .Select(p => $"pair {p.N} has an unknown driver"));
        problems.AddRange(content.Pairs.Select((p, i) => (p, i)).Where(x => x.p.N != x.i + 1).Select(x => $"pair {x.p.N} is out of order"));
        problems.AddRange(content.Islands.Where(i => i.From < 1 || i.To > content.Pairs.Count || i.From > i.To).Select(i => $"island {i.N} is out of range"));
        problems.AddRange(Drivers.All.Where(d => content.FindDriver(d) is null).Select(d => $"driver {d} has no description"));
        problems.AddRange(content.Archetypes
            .Where(a => a.Dominant.Concat(a.Secondary).Concat(a.EitherOf ?? []).Any(d => !Drivers.IsDriver(d)))
            .Select(a => $"archetype {a.Id} has an unknown driver"));
        if (content.Archetypes.Count == 0)
        {
            problems.Add("no archetypes");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Career Drivers content is invalid: " + string.Join("; ", problems));
        }
    }
}
