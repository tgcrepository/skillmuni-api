using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muni.Application;
using SkillMuni.Common.Tenancy;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Tenancy;

namespace Muni.Infrastructure.External;

/// <summary>Learner's internal profile-progress API (internal ingress). A failure never blocks a reply.</summary>
internal sealed partial class LearnerProfileProgressClient(HttpClient http, ITenantContext tenant, ILogger<LearnerProfileProgressClient> logger) : IProfileProgressClient
{
    public async Task<ProfileProgress?> GetAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/learners/{learnerId}/profile-progress");
            request.Headers.Add(TenancyOptions.TenantHeader, tenant.TenantId);
            using var response = await http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProfileProgress>(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, e.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Learner profile progress unavailable {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string reason);
}

/// <summary>
/// Learner's <c>POST api/v1/profile/reveal</c>, called on the learner's behalf: their own access token is forwarded,
/// so Learner checks the session itself and only ever returns the caller's own details. Values are never logged or cached.
/// </summary>
internal sealed partial class LearnerPrivateFieldClient(HttpClient http, ILogger<LearnerPrivateFieldClient> logger) : IPrivateFieldClient
{
    public async Task<string?> RevealAsync(string field, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/profile/reveal", new { field }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<RevealDto>(cancellationToken);
            return body?.Value ?? string.Empty;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, field, e.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Learner reveal unavailable {Field} {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string field, string reason);
}

/// <summary>Copies the caller's access token (cookie or bearer header) and tenant onto the outgoing request.</summary>
internal sealed class ForwardSessionHandler(IHttpContextAccessor accessor, IOptions<AuthOptions> auth, ITenantContext tenant) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var http = accessor.HttpContext;
        var token = http?.Request.Cookies[auth.Value.AccessCookieName];
        if (string.IsNullOrEmpty(token))
        {
            var header = http?.Request.Headers.Authorization.ToString();
            token = header is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Remove(TenancyOptions.TenantHeader);
        request.Headers.Add(TenancyOptions.TenantHeader, tenant.TenantId);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Development and tests without Learner running: the mock's values (muni.handlers.ts <c>PROFILE</c>).</summary>
internal sealed class SandboxPrivateFieldClient : IPrivateFieldClient
{
    public const string Email = "yash.pohare@thegamification.com";
    public const string Mobile = "+91 98765 40697";

    public Task<string?> RevealAsync(string field, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(field == RevealFields.Email ? Email : Mobile);
}

/// <summary>Development and tests without Learner running: half the job profile done, one quest step left.</summary>
internal sealed class SandboxProfileProgressClient : IProfileProgressClient
{
    public Task<ProfileProgress?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<ProfileProgress?>(new ProfileProgress(
            50,
            [
                new ProfileQuest(1, "who-dis", "Who Dis?", []),
                new ProfileQuest(4, "your-flex-era", "Your Flex Era", [new ProfileStep("Languages", 3)]),
            ]));
}
