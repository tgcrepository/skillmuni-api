using Microsoft.AspNetCore.Http;
using SkillMuni.Gateway;

namespace SkillMuni.Gateway.UnitTests;

public sealed class SecurityHeadersTests
{
    [Fact]
    public void Api_responses_get_the_security_headers_and_lose_the_server_banner()
    {
        var headers = new HeaderDictionary { ["Server"] = "Kestrel", ["X-Powered-By"] = "ASP.NET" };

        SecurityHeadersMiddleware.Apply(headers, https: false, relaxedCsp: false);

        Assert.False(headers.ContainsKey("Server"));
        Assert.False(headers.ContainsKey("X-Powered-By"));
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public void Https_adds_hsts_and_swagger_keeps_its_own_csp()
    {
        var headers = new HeaderDictionary();

        SecurityHeadersMiddleware.Apply(headers, https: true, relaxedCsp: true);

        Assert.StartsWith("max-age=", headers.StrictTransportSecurity.ToString(), StringComparison.Ordinal);
        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }
}
