using System.Text.Json;
using System.Text.Json.Serialization;
using Assessment.Domain;

namespace Assessment.Application;

// Request and response shapes of the frontend's assessment.api.ts and stats.api.ts (JSON camelCase).

// ---------- Requests ----------

/// <summary>API-005 auto-save: absolute values, so a replay changes nothing (BR-133).</summary>
public sealed record SaveAnswersRequest(
    [property: JsonConverter(typeof(AnswerListJsonConverter))] IReadOnlyList<Answer>? Answers,
    int? Current);

/// <summary>API-006 "Open my jars".</summary>
public sealed record SubmitAnswersRequest(
    [property: JsonConverter(typeof(AnswerListJsonConverter))] IReadOnlyList<Answer>? Answers);

/// <summary>API-010.</summary>
public sealed record RemindRequest(bool? Remind);

// ---------- Content ----------

public sealed record StatementDto(string Text, string Driver);

public sealed record PairDto(int N, StatementDto This, StatementDto That);

public sealed record IslandDto(int N, string Name, string Emoji, int From, int To);

public sealed record DriverInfoDto(
    string Id,
    string Name,
    string Emoji,
    string Focus,
    string Definition,
    string Blurb,
    string StrengthZones,
    string Families);

public sealed record ArchetypeDto(
    string Id,
    string Name,
    string Emoji,
    IReadOnlyList<string> Dominant,
    IReadOnlyList<string> Secondary,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? EitherOf,
    string Roles,
    string RedFlags,
    string Summary);

// ---------- Responses ----------

/// <summary><c>status</c>: new · in-progress · done.</summary>
public sealed record AssessmentStateDto(
    string Status,
    IReadOnlyList<PairDto> Pairs,
    IReadOnlyList<IslandDto> Islands,
    [property: JsonConverter(typeof(AnswerListJsonConverter))] IReadOnlyList<Answer> Answers,
    int Current,
    int Xp,
    bool EarnsXp,
    bool Remind,
    int Attempts);

/// <param name="XpEarned">XP just earned by this save (an island cleared).</param>
public sealed record SaveAnswersResponse(int Xp, int XpEarned);

public sealed record AttemptRefDto(int N, string Date, string Archetype);

/// <param name="Scores">Driver id → points (0–24), A–I.</param>
/// <param name="Ranking">Highest first; ties keep survey order.</param>
/// <param name="Attempts">Newest first.</param>
public sealed record AssessmentResultDto(
    int Attempt,
    string Date,
    IReadOnlyDictionary<string, int> Scores,
    IReadOnlyList<string> Ranking,
    ArchetypeDto Archetype,
    IReadOnlyList<DriverInfoDto> Drivers,
    IReadOnlyDictionary<string, int>? Previous,
    IReadOnlyList<AttemptRefDto> Attempts,
    int XpEarned,
    int MandatoryCourses);

public sealed record RemindResponse(bool Remind);

// ---------- My Stats → Assessments (stats.api.ts) ----------

/// <param name="Tier">beginner · intermediate · advanced · expert.</param>
public sealed record SkillAssessmentResultDto(
    string Id,
    string Title,
    string Subtitle,
    string Tier,
    int Score,
    int Correct,
    int Total,
    string Completed,
    string RetakeUrl);

public sealed record RecommendedAssessmentDto(string Title, string Url, string Why);

public sealed record AssessmentsDashboardDto(
    IReadOnlyList<SkillAssessmentResultDto> Results,
    int Available,
    int FirstCardXp,
    RecommendedAssessmentDto Recommended);

/// <summary>
/// The frontend's <c>Answer</c> union: a notch <c>0</c>–<c>3</c>, <c>"skip"</c> or <c>null</c>. Anything else is a
/// malformed request (400).
/// </summary>
public sealed class AnswerListJsonConverter : JsonConverter<IReadOnlyList<Answer>>
{
    public const string SkipToken = "skip";

    public override IReadOnlyList<Answer> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Answers must be an array.");
        }

        var answers = new List<Answer>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            answers.Add(reader.TokenType switch
            {
                JsonTokenType.Null => Answer.None,
                JsonTokenType.Number when reader.TryGetInt32(out var notch) && notch is >= 0 and <= 3 => Answer.FromNotch(notch),
                JsonTokenType.String when reader.ValueTextEquals(SkipToken) => Answer.Skip,
                _ => throw new JsonException("An answer is 0, 1, 2, 3, \"skip\" or null."),
            });

            if (answers.Count > 1000)
            {
                throw new JsonException("Too many answers.");
            }
        }

        return answers;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<Answer> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var answer in value)
        {
            if (answer.Notch is { } notch)
            {
                writer.WriteNumberValue(notch);
            }
            else if (answer.IsSkip)
            {
                writer.WriteStringValue(SkipToken);
            }
            else
            {
                writer.WriteNullValue();
            }
        }

        writer.WriteEndArray();
    }
}
