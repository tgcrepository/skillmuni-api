using System.Text.Json;

namespace Careers.Domain;

/// <summary>
/// One row per learner (id = Identity account id), created on first contact: projections of other services' events
/// (assessment result, Job Profile at 100 %, institute claim) and the learner's Careers settings (alerts, visibility).
/// </summary>
public sealed class CareerLearner : Entity
{
    private CareerLearner()
    {
    }

    // ---- Projections (from events) ----

    public bool AssessmentDone { get; private set; }

    public int AssessmentAttempt { get; private set; }

    public string? ArchetypeName { get; private set; }

    public string? ArchetypeSummary { get; private set; }

    public string TopDriversJson { get; private set; } = "[]";

    public DateTime? AssessmentTakenAt { get; private set; }

    /// <summary><c>learner.job-profile.completed</c>: the "Finish job profile" mission (BR-110).</summary>
    public DateTime? JobProfileCompletedAt { get; private set; }

    /// <summary><c>identity.claim.confirmed</c>: matches show before the profile is published (BR-72).</summary>
    public DateTime? ClaimConfirmedAt { get; private set; }

    // ---- Settings ----

    /// <summary>Last visit to My Jobs (clears the Launchpad's "new matches", BR-62).</summary>
    public DateTime? JobsSeenAt { get; private set; }

    public bool JobAlertsOn { get; private set; }

    public string JobAlertsFrequency { get; private set; } = "daily";

    public string JobAlertSearchesJson { get; private set; } = "[]";

    public bool FestAlertsCity { get; private set; }

    public bool FestAlertsOnline { get; private set; }

    /// <summary>My Matches visibility (Vibe Check "Send it"); null until published.</summary>
    public DateTime? PublishedAt { get; private set; }

    public bool Discoverable { get; private set; }

    public bool InvitesAllowed { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<string> TopDrivers => JsonSerializer.Deserialize<List<string>>(TopDriversJson) ?? [];

    public IReadOnlyList<string> JobAlertSearches => JsonSerializer.Deserialize<List<string>>(JobAlertSearchesJson) ?? [];

    public static CareerLearner Start(Guid learnerId, string tenantId, DateTime now) => new()
    {
        Id = learnerId,
        TenantId = tenantId,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>A newer (or equal) attempt replaces the result; an older one arriving late is ignored.</summary>
    public void AssessmentSubmitted(int attempt, string archetypeName, string archetypeSummary, IReadOnlyList<string> topDrivers, DateTime takenAt, DateTime now)
    {
        if (AssessmentDone && attempt < AssessmentAttempt)
        {
            return;
        }

        AssessmentDone = true;
        AssessmentAttempt = attempt;
        ArchetypeName = archetypeName;
        ArchetypeSummary = archetypeSummary;
        TopDriversJson = JsonSerializer.Serialize(topDrivers.Take(5).ToList());
        AssessmentTakenAt = takenAt;
        UpdatedAt = now;
    }

    public void JobProfileCompleted(DateTime at, DateTime now)
    {
        JobProfileCompletedAt ??= at;
        UpdatedAt = now;
    }

    public void ClaimConfirmed(DateTime at, DateTime now)
    {
        ClaimConfirmedAt ??= at;
        UpdatedAt = now;
    }

    public void SawJobs(DateTime now)
    {
        JobsSeenAt = now;
        UpdatedAt = now;
    }

    public void SetJobAlerts(bool on, string frequency, IReadOnlyList<string> searches, DateTime now)
    {
        JobAlertsOn = on;
        JobAlertsFrequency = frequency;
        JobAlertSearchesJson = JsonSerializer.Serialize(searches);
        UpdatedAt = now;
    }

    public void SetFestAlerts(bool city, bool online, DateTime now)
    {
        FestAlertsCity = city;
        FestAlertsOnline = online;
        UpdatedAt = now;
    }

    public void Publish(bool discoverable, bool invites, DateTime now)
    {
        Discoverable = discoverable;
        InvitesAllowed = invites;
        PublishedAt = now;
        UpdatedAt = now;
    }
}
