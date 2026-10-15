using Assessment.Domain;

namespace Assessment.Application.Services;

/// <summary>Maps the survey content to its response shapes.</summary>
internal static class ContentViews
{
    public static PairDto ToDto(this Pair p) =>
        new(p.N, new StatementDto(p.This.Text, p.This.Driver), new StatementDto(p.That.Text, p.That.Driver));

    public static IslandDto ToDto(this Island i) => new(i.N, i.Name, i.Emoji, i.From, i.To);

    public static DriverInfoDto ToDto(this DriverInfo d) =>
        new(d.Id, d.Name, d.Emoji, d.Focus, d.Definition, d.Blurb, d.StrengthZones, d.Families);

    public static ArchetypeDto ToDto(this Archetype a) =>
        new(a.Id, a.Name, a.Emoji, a.Dominant, a.Secondary, a.EitherOf, a.Roles, a.RedFlags, a.Summary);

    public static string ToIsoDate(this DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
