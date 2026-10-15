using System.Text.Json;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Correlation;
using SkillMuni.Web.Tenancy;

namespace SkillMuni.Gateway;

/// <summary>
/// Silent refresh (MISS-13). Access tokens live 10 minutes; when the access cookie is missing, expiring or older
/// than <see cref="GatewayOptions.RefreshWhenOlderThan"/>, the gateway trades the refresh cookie for a new pair,
/// forwards the request with the new access token and sets the new cookies on the response. The frontend keeps
/// its current contract: it never calls a refresh endpoint itself.
/// </summary>
public sealed class SessionRefreshMiddleware(
    RequestDelegate next,
    IOptions<AuthOptions> authOptions,
    IOptions<GatewayOptions> gatewayOptions,
    TimeProvider clock)
{
    private static readonly PathString ApiPath = new("/api");
    private static readonly PathString ExplicitRefreshPath = new("/api/v1/auth/refresh");

    public async Task InvokeAsync(HttpContext context, IdentitySessionClient identity)
    {
        var auth = authOptions.Value;
        if (!context.Request.Path.StartsWithSegments(ApiPath)
            || context.Request.Path.StartsWithSegments(ExplicitRefreshPath)
            || !context.Request.Cookies.TryGetValue(auth.RefreshCookieName, out var refreshToken)
            || string.IsNullOrEmpty(refreshToken))
        {
            await next(context);
            return;
        }

        context.Request.Cookies.TryGetValue(auth.AccessCookieName, out var accessToken);
        if (!NeedsRefresh(accessToken, clock.GetUtcNow(), gatewayOptions.Value.RefreshWhenOlderThan))
        {
            await next(context);
            return;
        }

        var tenant = context.Request.Headers[TenancyOptions.TenantHeader].ToString();
        var outcome = await identity.RefreshAsync(refreshToken, tenant, CorrelationIdMiddleware.Get(context), context.RequestAborted);

        switch (outcome)
        {
            case { Status: RefreshStatus.Refreshed, Tokens: { } tokens }:
                ReplaceRequestCookies(context.Request, new Dictionary<string, string?>
                {
                    [auth.AccessCookieName] = tokens.AccessToken,
                    [auth.RefreshCookieName] = tokens.RefreshToken ?? refreshToken,
                });
                context.Response.OnStarting(() =>
                {
                    SessionCookies.Append(context.Response, auth, tokens.AccessToken, tokens.AccessExpiresAt, tokens.RefreshToken, tokens.RefreshExpiresAt);
                    return Task.CompletedTask;
                });
                break;

            case { Status: RefreshStatus.Expired }:
                // Idle for 30 minutes, revoked or past its 30-day limit: forward without a session.
                ReplaceRequestCookies(context.Request, new Dictionary<string, string?>
                {
                    [auth.AccessCookieName] = null,
                    [auth.RefreshCookieName] = null,
                });
                context.Response.OnStarting(() =>
                {
                    SessionCookies.Delete(context.Response, auth);
                    return Task.CompletedTask;
                });
                break;

            default:
                // Identity unreachable: let the request through; a still-valid access token keeps working.
                break;
        }

        await next(context);
    }

    /// <summary>True when there is no usable access token, it expires within 30 s, or it is older than the threshold.</summary>
    public static bool NeedsRefresh(string? accessToken, DateTimeOffset now, TimeSpan olderThan)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return true;
        }

        var parts = accessToken.Split('.');
        if (parts.Length != 3)
        {
            return true;
        }

        try
        {
            var payload = Base64UrlDecode(parts[1]);
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (!root.TryGetProperty("exp", out var exp) || !root.TryGetProperty("iat", out var iat))
            {
                return true;
            }

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat.GetInt64());
            return expiresAt <= now.AddSeconds(30) || now - issuedAt >= olderThan;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            return true;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }

    /// <summary>Edits the raw Cookie header so other cookies pass through byte for byte.</summary>
    private static void ReplaceRequestCookies(HttpRequest request, IReadOnlyDictionary<string, string?> changes)
    {
        var kept = request.Headers.Cookie.ToString()
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pair =>
            {
                var name = pair.Split('=', 2)[0];
                return !changes.ContainsKey(name);
            })
            .ToList();

        kept.AddRange(changes.Where(c => c.Value is not null).Select(c => $"{c.Key}={c.Value}"));
        request.Headers.Cookie = string.Join("; ", kept);
    }
}
