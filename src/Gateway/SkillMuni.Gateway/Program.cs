using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Gateway;
using SkillMuni.Web.Correlation;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddSkillMuniPlatform("gateway");

var services = builder.Services;
services.AddOptions<GatewayOptions>().BindConfiguration(GatewayOptions.Section);
services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

services.Configure<ForwardedHeadersOptions>(o =>
{
    // Container Apps ingress sits in front of the gateway; trust its X-Forwarded-* headers.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

services.AddCors();
services.AddOptions<CorsOptions>().Configure<IOptions<GatewayOptions>>((cors, gateway) =>
    cors.AddDefaultPolicy(p => p
        .WithOrigins(gateway.Value.AllowedOrigins)
        .AllowCredentials()
        .AllowAnyHeader()
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

services.AddRateLimiter(_ => { });
services.AddOptions<RateLimiterOptions>().Configure<IOptions<GatewayOptions>>((limits, gateway) =>
{
    var settings = gateway.Value;
    limits.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limits.OnRejected = (context, cancellationToken) => new ValueTask(ApiProblems.WriteAsync(
        context.HttpContext,
        Error.RateLimited("too-many-requests", "Too many requests. Try again in a minute."),
        cancellationToken));
    limits.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => PerMinute(settings.GlobalPerMinute)));
    limits.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => PerMinute(settings.AuthPerMinute)));
});

services.AddHttpClient<IdentitySessionClient>((sp, http) =>
        http.BaseAddress = sp.GetRequiredService<IOptions<GatewayOptions>>().Value.IdentityBaseAddress)
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
    });

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<SessionRefreshMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // One Swagger UI over every service's OpenAPI document (proxied under /openapi/<service>/).
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/identity/v1.json", "Identity v1");
        o.SwaggerEndpoint("/openapi/learner/v1.json", "Learner v1");
        o.SwaggerEndpoint("/openapi/cv/v1.json", "CV v1");
        o.SwaggerEndpoint("/openapi/gamification/v1.json", "Gamification v1");
        o.SwaggerEndpoint("/openapi/assessment/v1.json", "Assessment v1");
        o.SwaggerEndpoint("/openapi/learning/v1.json", "Learning v1");
        o.SwaggerEndpoint("/openapi/muni/v1.json", "Muni v1");
        o.SwaggerEndpoint("/openapi/platform/v1.json", "Platform v1");
    });
}

app.MapSkillMuniHealth();
app.MapReverseProxy();

await app.RunAsync();

static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
{
    PermitLimit = permits,
    Window = TimeSpan.FromMinutes(1),
    QueueLimit = 0,
};

/// <summary>Entry point marker for WebApplicationFactory tests.</summary>
public partial class Program
{
}
