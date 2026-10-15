namespace Assessment.Domain;

/// <summary>
/// One pair's answer (Figma note 268:22368): a notch 0–3 picks one of the 4 valid 3-coin splits, 0 = All in This (3·0),
/// 1 = Lean This (2·1), 2 = Lean That (1·2), 3 = All in That (0·3). <see cref="None"/> = not reached yet,
/// <see cref="Skip"/> = skipped for now (asked again at the final check, A-213).
/// </summary>
public readonly record struct Answer
{
    // default(Answer) must read as "not reached", so the stored value is 0 for none, 1 for skip and 2–5 for notches 0–3.
    private const byte NoneValue = 0;
    private const byte SkipValue = 1;
    private const byte NotchOffset = 2;

    private readonly byte _value;

    private Answer(byte value)
    {
        _value = value;
    }

    public static Answer None => default;

    public static Answer Skip => new(SkipValue);

    public bool IsNone => _value == NoneValue;

    public bool IsSkip => _value == SkipValue;

    /// <summary>Only a notch counts; a skipped pair still blocks "Open my jars".</summary>
    public bool IsAnswered => _value >= NotchOffset;

    /// <summary>0–3 when answered.</summary>
    public int? Notch => IsAnswered ? _value - NotchOffset : null;

    public static Answer FromNotch(int notch) =>
        notch is >= 0 and <= 3 ? new Answer((byte)(notch + NotchOffset)) : throw new ArgumentOutOfRangeException(nameof(notch), notch, "A notch is 0 to 3.");

    /// <summary>Storage code: <c>0</c>–<c>3</c>, <c>s</c> (skip), <c>-</c> (not reached).</summary>
    public char ToCode() => Notch is { } notch ? (char)('0' + notch) : IsSkip ? 's' : '-';

    public static Answer FromCode(char code) => code switch
    {
        >= '0' and <= '3' => FromNotch(code - '0'),
        's' => Skip,
        _ => None,
    };

    public static string Encode(IEnumerable<Answer> answers) => new(answers.Select(a => a.ToCode()).ToArray());

    /// <summary>Always <paramref name="count"/> answers: missing ones are not reached, extra ones are dropped.</summary>
    public static IReadOnlyList<Answer> Decode(string? code, int count) =>
        Enumerable.Range(0, count).Select(i => code is not null && i < code.Length ? FromCode(code[i]) : None).ToList();

    public static IReadOnlyList<Answer> Empty(int count) => Enumerable.Repeat(None, count).ToList();
}

/// <summary>Points per driver, A–I. Each driver appears in 8 pairs, so a score runs 0–24 and all nine add up to 108 (BR-50).</summary>
public sealed class DriverScores : IEquatable<DriverScores>
{
    private readonly int[] _values;

    public DriverScores(IReadOnlyList<int> values)
    {
        if (values.Count != Drivers.All.Count)
        {
            throw new ArgumentException("Scores need one value per driver (A–I).", nameof(values));
        }

        _values = [.. values];
    }

    public IReadOnlyList<int> Values => _values;

    public int this[string driver] => _values[Drivers.IndexOf(driver)];

    public int Total => _values.Sum();

    /// <summary>Driver id → points, in survey order.</summary>
    public IReadOnlyDictionary<string, int> ToDictionary()
    {
        var map = new Dictionary<string, int>(Drivers.All.Count, StringComparer.Ordinal);
        for (var i = 0; i < _values.Length; i++)
        {
            map[Drivers.All[i]] = _values[i];
        }

        return map;
    }

    public bool Equals(DriverScores? other) => other is not null && _values.AsSpan().SequenceEqual(other._values);

    public override bool Equals(object? obj) => Equals(obj as DriverScores);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var v in _values)
        {
            hash.Add(v);
        }

        return hash.ToHashCode();
    }
}

/// <summary>The Career Drivers scoring rules (BR-50–52, A-211). Pure functions, so they are unit-tested on their own.</summary>
public static class CareerDriversScoring
{
    public const int CoinsPerPair = 3;

    /// <summary>Notch n gives the "this" driver 3 − n points and the "that" driver n points. Unanswered pairs give nothing.</summary>
    public static DriverScores Score(IReadOnlyList<Pair> pairs, IReadOnlyList<Answer> answers)
    {
        var values = new int[Drivers.All.Count];
        for (var i = 0; i < pairs.Count && i < answers.Count; i++)
        {
            if (answers[i].Notch is not { } notch)
            {
                continue;
            }

            values[Drivers.IndexOf(pairs[i].This.Driver)] += CoinsPerPair - notch;
            values[Drivers.IndexOf(pairs[i].That.Driver)] += notch;
        }

        return new DriverScores(values);
    }

    /// <summary>Highest first; ties keep survey order A → I (BR-51).</summary>
    public static IReadOnlyList<string> Rank(DriverScores scores) =>
        Drivers.All.Select((d, i) => (Driver: d, Index: i))
            .OrderByDescending(x => scores.Values[x.Index])
            .ThenBy(x => x.Index)
            .Select(x => x.Driver)
            .ToList();

    /// <summary>
    /// BR-52: the archetype whose 2 dominant drivers add up highest; ties go to the higher average of its secondary
    /// drivers ("± D or G" adds the stronger of the two as one more secondary), then to table order.
    /// </summary>
    public static Archetype Match(DriverScores scores, IReadOnlyList<Archetype> archetypes)
    {
        if (archetypes.Count == 0)
        {
            throw new ArgumentException("At least one archetype is needed.", nameof(archetypes));
        }

        return archetypes
            .Select((a, i) => (Archetype: a, Index: i, Dominant: a.Dominant.Sum(d => scores[d]), Secondary: SecondaryAverage(a, scores)))
            .OrderByDescending(x => x.Dominant)
            .ThenByDescending(x => x.Secondary)
            .ThenBy(x => x.Index)
            .First()
            .Archetype;
    }

    private static double SecondaryAverage(Archetype archetype, DriverScores scores)
    {
        var values = archetype.Secondary.Select(d => scores[d]).ToList();
        if (archetype.EitherOf is { Count: > 0 } either)
        {
            values.Add(either.Max(d => scores[d]));
        }

        return values.Count == 0 ? 0 : values.Average();
    }
}
