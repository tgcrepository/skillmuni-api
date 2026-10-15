using Microsoft.AspNetCore.Http;

namespace SkillMuni.Web.Auth;

/// <summary>
/// The two session cookies, written the same way by the Identity service and by the gateway's silent refresh:
/// HttpOnly, Secure, SameSite=Lax (CLAUDE.md §6). The refresh cookie only travels to <c>/api</c>.
/// </summary>
public static class SessionCookies
{
    public const string RefreshCookiePath = "/api";

    public static void Append(
        HttpResponse response,
        AuthOptions auth,
        string accessToken,
        DateTimeOffset accessExpiresAt,
        string? refreshToken,
        DateTimeOffset refreshExpiresAt)
    {
        response.Cookies.Append(auth.AccessCookieName, accessToken, Options("/", accessExpiresAt));
        if (refreshToken is not null)
        {
            response.Cookies.Append(auth.RefreshCookieName, refreshToken, Options(RefreshCookiePath, refreshExpiresAt));
        }
    }

    public static void Delete(HttpResponse response, AuthOptions auth)
    {
        response.Cookies.Delete(auth.AccessCookieName, Options("/", null));
        response.Cookies.Delete(auth.RefreshCookieName, Options(RefreshCookiePath, null));
    }

    private static CookieOptions Options(string path, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = path,
        Expires = expires,
        IsEssential = true,
    };
}
