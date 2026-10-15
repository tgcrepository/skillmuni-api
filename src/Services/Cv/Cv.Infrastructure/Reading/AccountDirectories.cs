using System.Net;
using System.Net.Http.Json;
using Cv.Application;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Tenancy;

namespace Cv.Infrastructure.Reading;

/// <summary>S9: asks Identity (internal ingress) who owns an email. A failure never blocks the parse.</summary>
internal sealed partial class IdentityAccountDirectory(HttpClient http, ITenantContext tenant, ILogger<IdentityAccountDirectory> logger) : IAccountDirectory
{
    public async Task<bool?> EmailBelongsToSomeoneElseAsync(string email, Guid learnerId, CancellationToken cancellationToken)
    {
        try
        {
            // POST with the email in the body: no personal data in URLs (they end up in logs).
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/accounts/email-owner")
            {
                Content = JsonContent.Create(new { email }),
            };
            request.Headers.Add("X-Tenant-Id", tenant.TenantId);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            var owner = await response.Content.ReadFromJsonAsync<EmailOwner>(cancellationToken);
            return owner is not null && owner.AccountId != learnerId;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, e.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity email lookup unavailable {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string reason);

    private sealed record EmailOwner(Guid AccountId);
}

/// <summary>Development and tests: any address starting with "taken" belongs to someone else.</summary>
internal sealed class SandboxAccountDirectory : IAccountDirectory
{
    public Task<bool?> EmailBelongsToSomeoneElseAsync(string email, Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(email.StartsWith("taken", StringComparison.OrdinalIgnoreCase));
}
