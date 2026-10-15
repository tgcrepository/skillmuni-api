using System.Text.Json;

namespace SkillMuni.Gateway.UnitTests;

/// <summary>
/// The gateway proxies only the public API. Internal endpoints (<c>/internal/…</c>) are reachable only inside the
/// Container Apps environment (internal ingress), never through the gateway.
/// </summary>
public sealed class RouteTableTests
{
    private static readonly Lazy<IReadOnlyList<(string Name, string Cluster, string Path)>> Routes = new(Load);

    [Fact]
    public void Every_route_is_under_api_v1()
    {
        Assert.NotEmpty(Routes.Value);
        Assert.All(Routes.Value, r => Assert.StartsWith("/api/v1/", r.Path, StringComparison.Ordinal));
        Assert.DoesNotContain(Routes.Value, r => r.Path.Contains("internal", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/api/v1/me/progress", "gamification")]
    [InlineData("/api/v1/job-profile/{**rest}", "learner")]
    public void Routes_go_to_the_owning_service(string path, string cluster) =>
        Assert.Equal(cluster, Assert.Single(Routes.Value, r => r.Path == path).Cluster);

    private static IReadOnlyList<(string, string, string)> Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Gateway", "SkillMuni.Gateway", "appsettings.json")))
        {
            dir = dir.Parent;
        }

        var file = dir is null
            ? Path.Combine(AppContext.BaseDirectory, "appsettings.json")
            : Path.Combine(dir.FullName, "src", "Gateway", "SkillMuni.Gateway", "appsettings.json");
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        return json.RootElement.GetProperty("ReverseProxy").GetProperty("Routes").EnumerateObject()
            .Select(r => (r.Name, r.Value.GetProperty("ClusterId").GetString()!, r.Value.GetProperty("Match").GetProperty("Path").GetString()!))
            .ToList();
    }
}
