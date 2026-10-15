using System.Text;
using System.Text.Json;
using SkillMuni.Gateway;

namespace SkillMuni.Gateway.UnitTests;

public sealed class SessionRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Threshold = TimeSpan.FromMinutes(2);

    private static string Token(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        static string Part(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Part(new { alg = "RS256" })}.{Part(new { iat = issuedAt.ToUnixTimeSeconds(), exp = expiresAt.ToUnixTimeSeconds() })}.signature";
    }

    [Fact]
    public void Missing_access_token_needs_a_refresh() =>
        Assert.True(SessionRefreshMiddleware.NeedsRefresh(null, Now, Threshold));

    [Fact]
    public void Fresh_token_is_left_alone() =>
        Assert.False(SessionRefreshMiddleware.NeedsRefresh(Token(Now.AddSeconds(-30), Now.AddMinutes(9)), Now, Threshold));

    [Fact]
    public void Token_older_than_the_threshold_is_refreshed_to_record_activity() =>
        Assert.True(SessionRefreshMiddleware.NeedsRefresh(Token(Now.AddMinutes(-3), Now.AddMinutes(7)), Now, Threshold));

    [Fact]
    public void Token_about_to_expire_is_refreshed() =>
        Assert.True(SessionRefreshMiddleware.NeedsRefresh(Token(Now.AddSeconds(-10), Now.AddSeconds(20)), Now, Threshold));

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("a.e30.c")]
    public void Unreadable_token_is_refreshed(string token) =>
        Assert.True(SessionRefreshMiddleware.NeedsRefresh(token, Now, Threshold));
}
