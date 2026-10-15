using System.Net;
using SkillMuni.Web.Correlation;
using SkillMuni.Web.Tenancy;

namespace SkillMuni.Gateway;

public sealed record RefreshedTokens(string AccessToken, DateTimeOffset AccessExpiresAt, string? RefreshToken, DateTimeOffset RefreshExpiresAt);

public enum RefreshStatus
{
    Refreshed,
    Expired,
    Unavailable,
}

public sealed record RefreshOutcome(RefreshStatus Status, RefreshedTokens? Tokens);

/// <summary>Typed client for Identity's internal refresh endpoint (retry, timeout and circuit breaker via resilience handler).</summary>
public sealed class IdentitySessionClient(HttpClient http, ILogger<IdentitySessionClient> logger)
{
    public async Task<RefreshOutcome> RefreshAsync(string refreshToken, string tenantId, string? correlationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/sessions/refresh")
        {
            Content = JsonContent.Create(new { refreshToken }),
        };
        request.Headers.Add(TenancyOptions.TenantHeader, tenantId);
        if (correlationId is not null)
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new RefreshOutcome(RefreshStatus.Expired, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Session refresh returned {StatusCode}", (int)response.StatusCode);
                return new RefreshOutcome(RefreshStatus.Unavailable, null);
            }

            var tokens = await response.Content.ReadFromJsonAsync<RefreshedTokens>(cancellationToken);
            return tokens is null
                ? new RefreshOutcome(RefreshStatus.Unavailable, null)
                : new RefreshOutcome(RefreshStatus.Refreshed, tokens);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Session refresh failed");
            return new RefreshOutcome(RefreshStatus.Unavailable, null);
        }
    }
}
