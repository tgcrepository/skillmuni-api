using Careers.Domain;

namespace Careers.Application.Abstractions;

// Every query is scoped to the current tenant by the DbContext. Lists are read without tracking; Get/Find return
// tracked entities for the use case to change and save.

public interface ICareerLearnerRepository
{
    Task<CareerLearner?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(CareerLearner learner);
}

public interface ISavedJobRepository
{
    /// <summary>Job id → when it was saved.</summary>
    Task<IReadOnlyDictionary<string, DateTime>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<SavedJob?> FindAsync(Guid learnerId, string jobId, CancellationToken cancellationToken);

    void Add(SavedJob saved);

    void Remove(SavedJob saved);
}

public interface IApplicationRepository
{
    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<JobApplication>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<JobApplication?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken);

    /// <summary>The learner's latest application sent from My Jobs for this job, if any.</summary>
    Task<JobApplication?> LatestForJobAsync(Guid learnerId, string jobId, CancellationToken cancellationToken);

    Task<bool> FestOfferExistsAsync(Guid learnerId, string festOfferKey, CancellationToken cancellationToken);

    void Add(JobApplication application);
}

public interface ICvFileRepository
{
    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<CvFile>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<CvFile?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken);

    void Add(CvFile file);
}

public interface IInvitationRepository
{
    Task<IReadOnlyList<Invitation>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Invitation?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken);

    void Add(Invitation invitation);
}

/// <summary>Seats taken across the tenant: bookings per fest and interviews per (fest, employer).</summary>
public sealed record FestUsage(IReadOnlyDictionary<string, int> Bookings, IReadOnlyDictionary<(string FestId, string EmployerId), int> Interviews)
{
    public static readonly FestUsage None = new(new Dictionary<string, int>(), new Dictionary<(string, string), int>());

    public int BookingsFor(string festId) => Bookings.GetValueOrDefault(festId);

    public int InterviewsFor(string festId, string employerId) => Interviews.GetValueOrDefault((festId, employerId));
}

public interface IFestBookingRepository
{
    /// <summary>The learner's bookings with their picks, oldest first.</summary>
    Task<IReadOnlyList<FestBooking>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<FestBooking?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken);

    Task<FestBooking?> FindByFestAsync(Guid learnerId, string festId, CancellationToken cancellationToken);

    /// <summary>Interviews held per (employer, date, time) at one fest, across the tenant.</summary>
    Task<IReadOnlyDictionary<(string EmployerId, DateOnly Date, string Time), int>> SlotUseAsync(string festId, CancellationToken cancellationToken);

    Task<FestUsage> UsageAsync(IReadOnlyCollection<string> festIds, CancellationToken cancellationToken);

    void Add(FestBooking booking);

    void Remove(FestBooking booking);
}

public interface IFestActivityRepository
{
    Task<IReadOnlySet<string>> ListSavedAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<SavedFest?> FindSavedAsync(Guid learnerId, string festId, CancellationToken cancellationToken);

    void Add(SavedFest saved);

    void Remove(SavedFest saved);

    Task<FestWaitlistEntry?> FindWaitlistAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken);

    /// <summary>Entries for this employer that joined at or before <paramref name="joinedAt"/> (all when null).</summary>
    Task<int> CountWaitlistAsync(string festId, string employerId, DateTime? joinedAt, CancellationToken cancellationToken);

    void Add(FestWaitlistEntry entry);

    Task<IReadOnlyList<FestOutcome>> ListOutcomesAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<FestOutcome?> FindOutcomeAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken);

    void Add(FestOutcome outcome);

    void Remove(FestOutcome outcome);

    Task<IReadOnlyList<FestRating>> ListRatingsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<FestRating?> FindRatingAsync(Guid learnerId, string festId, CancellationToken cancellationToken);

    void Add(FestRating rating);
}

public interface IMatchStateRepository
{
    /// <summary>Match id → status.</summary>
    Task<IReadOnlyDictionary<string, string>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<MatchState?> FindAsync(Guid learnerId, string matchId, CancellationToken cancellationToken);

    void Add(MatchState state);
}

/// <summary>MISS-08 erasure: every row this service holds for one learner. Safe to run twice.</summary>
public interface ICareersDataEraser
{
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

// ---- Learner service (internal API, typed HttpClient with retry, timeout and circuit breaker) ----

public sealed record QuestStepGap(string Key, string Label, int Part);

public sealed record QuestFill(int Id, string Slug, string Name, int Filled, int Total, IReadOnlyList<QuestStepGap> Left);

/// <summary>A-262 Job Profile completeness, quest by quest (Learner's <c>profile-progress</c>).</summary>
public sealed record ProfileProgress(int Percent, int Filled, int Total, IReadOnlyList<QuestFill> Quests)
{
    public static readonly ProfileProgress Empty = new(0, 0, 0, []);

    public IReadOnlyList<QuestFill> WithGaps => Quests.Where(q => q.Left.Count > 0).ToList();
}

public enum LookupStatus
{
    Found,

    /// <summary>Learner has no profile for this account yet (0 %).</summary>
    NotFound,

    /// <summary>Learner didn't answer (timeout, 5xx, open circuit).</summary>
    Unavailable,
}

public sealed record ProgressLookup(LookupStatus Status, ProfileProgress Progress)
{
    public static readonly ProgressLookup Unavailable = new(LookupStatus.Unavailable, ProfileProgress.Empty);

    public static readonly ProgressLookup Missing = new(LookupStatus.NotFound, ProfileProgress.Empty);
}

/// <summary>A skill on the learner's profile and the evidence behind it: assessed · verified · declared · level.</summary>
public sealed record LearnerSkill(string Name, string Evidence);

/// <summary>
/// What matching and the apply dialog need from the learner's profile. Learner doesn't serve it yet (proposed
/// <c>GET internal/learners/{id}/careers-profile</c>); until it does, the lookup returns null and matches are unknown.
/// </summary>
public sealed record LearnerCareerProfile(
    string? Name,
    string? Contact,
    string? City,
    string? State,
    bool? Relocate,
    IReadOnlyList<LearnerSkill> Skills);

public interface ILearnerDirectory
{
    Task<ProgressLookup> ProgressAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <returns>Null when Learner can't say (endpoint missing, learner unknown or Learner down).</returns>
    Task<LearnerCareerProfile?> CareerProfileAsync(Guid learnerId, CancellationToken cancellationToken);
}
