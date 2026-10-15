using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Learner.Application.Services;

/// <summary>
/// Read-only, forgiving access to one quest's answers. The shapes are owned by the quest screens, so a missing or
/// differently typed field reads as empty instead of failing the Job Profile.
/// </summary>
internal sealed class QuestAnswerView
{
    private readonly JsonObject _values;

    public QuestAnswerView(JsonObject? values) => _values = values ?? new JsonObject();

    public static QuestAnswerView Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new QuestAnswerView(null);
        }

        try
        {
            return new QuestAnswerView(JsonNode.Parse(json) as JsonObject);
        }
        catch (JsonException)
        {
            return new QuestAnswerView(null);
        }
    }

    public JsonObject Raw => _values;

    public bool IsEmpty => _values.Count == 0;

    public string Str(string key)
    {
        if (_values[key] is not JsonValue value)
        {
            return string.Empty;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text.Trim();
        }

        return value.TryGetValue<double>(out var number) ? number.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    /// <summary>The first non-empty of several possible field names.</summary>
    public string Str(params string[] keys) => keys.Select(Str).FirstOrDefault(v => v.Length > 0) ?? string.Empty;

    public bool? Bool(string key) => _values[key] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    public int Int(string key)
    {
        if (_values[key] is not JsonValue value)
        {
            return 0;
        }

        if (value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    public IReadOnlyList<string> Strings(string key) =>
        _values[key] is JsonArray array
            ? array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s.Trim() : string.Empty).Where(s => s.Length > 0).ToList()
            : [];

    public IReadOnlyList<QuestAnswerView> Items(string key) =>
        _values[key] is JsonArray array ? array.OfType<JsonObject>().Select(o => new QuestAnswerView(o)).ToList() : [];
}
