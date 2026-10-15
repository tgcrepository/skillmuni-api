using Microsoft.Net.Http.Headers;

namespace SkillMuni.Gateway;

/// <summary>
/// Security headers on every response the browser gets (the API serves JSON only): no MIME sniffing, no framing,
/// no referrer, a locked-down CSP, HSTS over HTTPS, and no server banner (Kestrel's or a service's).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private static readonly PathString SwaggerPath = new("/swagger");

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(
            static state =>
            {
                var http = (HttpContext)state;
                Apply(http.Response.Headers, http.Request.IsHttps, http.Request.Path.StartsWithSegments(SwaggerPath));
                return Task.CompletedTask;
            },
            context);
        return next(context);
    }

    /// <param name="headers">The response headers.</param>
    /// <param name="https">HSTS only means something over HTTPS.</param>
    /// <param name="relaxedCsp">The development Swagger UI needs its own scripts and styles.</param>
    public static void Apply(IHeaderDictionary headers, bool https, bool relaxedCsp)
    {
        headers.Remove(HeaderNames.Server);
        headers.Remove(HeaderNames.XPoweredBy);
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (!relaxedCsp)
        {
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
        }

        if (https)
        {
            headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        }
    }
}
