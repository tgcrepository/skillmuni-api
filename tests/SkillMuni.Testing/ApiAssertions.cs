using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SkillMuni.Testing;

public static class ApiAssertions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {body}");
        return string.IsNullOrEmpty(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Asserts the RFC 7807 shape the frontend reads and returns <c>errors.code[0]</c>.</summary>
    public static async Task<string> ProblemCodeAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.JsonAsync(expected);
        Assert.Equal(JsonValueKind.String, json.GetProperty("message").ValueKind);
        return json.GetProperty("errors").GetProperty("code")[0].GetString()!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body);
}
