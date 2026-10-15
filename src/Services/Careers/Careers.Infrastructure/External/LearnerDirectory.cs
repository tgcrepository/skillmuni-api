using System.Net;
using System.Net.Http.Json;
using Careers.Application.Abstractions;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Tenancy;

namespace Careers.Infrastructure.External;

/// <summary>
/// Learner's internal API (internal ingress, <c>X-Tenant-Id</c>), through a typed HttpClient with retry, timeout and
/// circuit breaker. Failures never throw into a screen: progress reports <see cref="LookupStatus.Unavailable"/> and the
/// career profile is null (unknown).
/// </summary>
internal sealed partial class LearnerDirectory(HttpClient http, ITenantContext tenant, ILogger<LearnerDirectory> logger) : ILearnerDirectory
{
    public async Task<ProgressLookup> ProgressAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync($"internal/learners/{learnerId}/profile-progress", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ProgressLookup.Missing;
            }

            response.EnsureSuccessStatusCode();
            var progress = await response.Content.ReadFromJsonAsync<ProfileProgress>(cancellationToken);
            return progress is null ? ProgressLookup.Missing : new ProgressLookup(LookupStatus.Found, progress);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, "profile-progress", e.GetType().Name);
            return ProgressLookup.Unavailable;
        }
    }

    public async Task<LearnerCareerProfile?> CareerProfileAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        try
        {
            // Proposed endpoint (not served by Learner yet): a 404 means "unknown", so matches stay empty.
            using var response = await SendAsync($"internal/learners/{learnerId}/careers-profile", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var profile = await response.Content.ReadFromJsonAsync<LearnerCareerProfile>(cancellationToken);
            return profile is null ? null : profile with { Skills = profile.Skills ?? [] };
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, "careers-profile", e.GetType().Name);
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Tenant-Id", tenant.TenantId);
        return await http.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Learner lookup unavailable {Lookup} {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string lookup, string reason);
}
