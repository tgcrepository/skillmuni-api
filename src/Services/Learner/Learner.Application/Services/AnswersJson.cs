using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Learner.Application.Services;

/// <summary>Quest answers are a flat JSON object owned by the quest screens; the server only merges and reads flags.</summary>
internal static class AnswersJson
{
    public const string Empty = "{}";

    /// <summary>Shallow merge: each top-level field in <paramref name="values"/> replaces the stored one.</summary>
    public static string Merge(string stored, JsonElement values)
    {
        var target = ParseObject(stored);
        foreach (var property in values.EnumerateObject())
        {
            target[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        return target.ToJsonString();
    }

    /// <summary>Adds fields from <paramref name="values"/> that aren't stored yet; stored fields are kept as they are.</summary>
    public static string Seed(string stored, JsonElement values)
    {
        var target = ParseObject(stored);
        if (values.ValueKind != JsonValueKind.Object)
        {
            return target.ToJsonString();
        }

        foreach (var property in values.EnumerateObject())
        {
            if (!target.ContainsKey(property.Name))
            {
                target[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        return target.ToJsonString();
    }

    public static JsonElement ToElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>A-262: the quest screens store <c>&lt;part&gt;Skipped: true</c> for an optional step left empty.</summary>
    public static bool IsSkipped(string json, string partKey) =>
        ParseObject(json)[$"{partKey}Skipped"] is JsonValue value && value.TryGetValue<bool>(out var skipped) && skipped;

    public static string? ReadString(JsonElement values, string name) =>
        values.ValueKind == JsonValueKind.Object && values.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static int Utf8Length(string json) => Encoding.UTF8.GetByteCount(json);

    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? Empty : json) as JsonObject ?? new JsonObject();
}
