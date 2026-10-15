using Careers.Application.Abstractions;
using Careers.Domain;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Storage;

namespace Careers.UnitTests.Fakes;

/// <summary>Every Careers repository over lists, plus the outbox, unit of work and blob store.</summary>
public sealed class InMemoryCareersStore :
    ICareerLearnerRepository,
    ISavedJobRepository,
    IApplicationRepository,
    ICvFileRepository,
    IInvitationRepository,
    IFestBookingRepository,
    IFestActivityRepository,
    IMatchStateRepository,
    ICareersDataEraser,
    IUnitOfWork,
    IOutbox,
    IBlobStore
{
    public List<CareerLearner> Learners { get; } = [];

    public List<SavedJob> SavedJobs { get; } = [];

    public List<JobApplication> Applications { get; } = [];

    public List<CvFile> CvFiles { get; } = [];

    public List<Invitation> Invitations { get; } = [];

    public List<FestBooking> Bookings { get; } = [];

    public List<FestWaitlistEntry> Waitlist { get; } = [];

    public List<FestOutcome> Outcomes { get; } = [];

    public List<FestRating> Ratings { get; } = [];

    public List<SavedFest> SavedFests { get; } = [];

    public List<MatchState> MatchStates { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public Dictionary<string, byte[]> Blobs { get; } = [];

    public int Saves { get; private set; }

    // ---- Learners ----
    Task<CareerLearner?> ICareerLearnerRepository.GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Learners.FirstOrDefault(l => l.Id == learnerId));

    public void Add(CareerLearner learner) => Learners.Add(learner);

    // ---- Saved jobs ----
    Task<IReadOnlyDictionary<string, DateTime>> ISavedJobRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, DateTime>>(SavedJobs.Where(s => s.LearnerId == learnerId).ToDictionary(s => s.JobId, s => s.SavedAt));

    public Task<SavedJob?> FindAsync(Guid learnerId, string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(SavedJobs.FirstOrDefault(s => s.LearnerId == learnerId && s.JobId == jobId));

    public void Add(SavedJob saved) => SavedJobs.Add(saved);

    public void Remove(SavedJob saved) => SavedJobs.Remove(saved);

    // ---- Applications ----
    Task<IReadOnlyList<JobApplication>> IApplicationRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JobApplication>>(Applications.Where(a => a.LearnerId == learnerId).OrderByDescending(a => a.AppliedAt).ToList());

    Task<JobApplication?> IApplicationRepository.GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Applications.FirstOrDefault(a => a.LearnerId == learnerId && a.Id == id));

    public Task<JobApplication?> LatestForJobAsync(Guid learnerId, string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Applications.Where(a => a.LearnerId == learnerId && a.JobId == jobId && a.Source == ApplicationSource.Apply).OrderByDescending(a => a.AppliedAt).FirstOrDefault());

    public Task<bool> FestOfferExistsAsync(Guid learnerId, string festOfferKey, CancellationToken cancellationToken) =>
        Task.FromResult(Applications.Any(a => a.LearnerId == learnerId && a.FestOfferKey == festOfferKey));

    public void Add(JobApplication application) => Applications.Add(application);

    // ---- CV files ----
    Task<IReadOnlyList<CvFile>> ICvFileRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CvFile>>(CvFiles.Where(f => f.LearnerId == learnerId).OrderByDescending(f => f.UploadedAt).ToList());

    Task<CvFile?> ICvFileRepository.GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(CvFiles.FirstOrDefault(f => f.LearnerId == learnerId && f.Id == id));

    public void Add(CvFile file) => CvFiles.Add(file);

    // ---- Invitations ----
    Task<IReadOnlyList<Invitation>> IInvitationRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Invitation>>(Invitations.Where(i => i.LearnerId == learnerId).OrderBy(i => i.CreatedAt).ToList());

    Task<Invitation?> IInvitationRepository.GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Invitations.FirstOrDefault(i => i.LearnerId == learnerId && i.Id == id));

    public void Add(Invitation invitation) => Invitations.Add(invitation);

    // ---- Fest bookings ----
    Task<IReadOnlyList<FestBooking>> IFestBookingRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FestBooking>>(Bookings.Where(b => b.LearnerId == learnerId).OrderBy(b => b.CreatedAt).ToList());

    Task<FestBooking?> IFestBookingRepository.GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Bookings.FirstOrDefault(b => b.LearnerId == learnerId && b.Id == id));

    public Task<FestBooking?> FindByFestAsync(Guid learnerId, string festId, CancellationToken cancellationToken) =>
        Task.FromResult(Bookings.FirstOrDefault(b => b.LearnerId == learnerId && b.FestId == festId));

    public Task<IReadOnlyDictionary<(string EmployerId, DateOnly Date, string Time), int>> SlotUseAsync(string festId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<(string EmployerId, DateOnly Date, string Time), int>>(Bookings
            .SelectMany(b => b.Picks)
            .Where(p => p.FestId == festId)
            .GroupBy(p => (p.EmployerId, p.Date, p.Time))
            .ToDictionary(g => g.Key, g => g.Count()));

    public Task<FestUsage> UsageAsync(IReadOnlyCollection<string> festIds, CancellationToken cancellationToken) =>
        Task.FromResult(new FestUsage(
            Bookings.Where(b => festIds.Contains(b.FestId)).GroupBy(b => b.FestId).ToDictionary(g => g.Key, g => g.Count()),
            Bookings.SelectMany(b => b.Picks).Where(p => festIds.Contains(p.FestId)).GroupBy(p => (p.FestId, p.EmployerId)).ToDictionary(g => g.Key, g => g.Count())));

    public void Add(FestBooking booking) => Bookings.Add(booking);

    public void Remove(FestBooking booking) => Bookings.Remove(booking);

    // ---- Fest activity ----
    public Task<IReadOnlySet<string>> ListSavedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(SavedFests.Where(s => s.LearnerId == learnerId).Select(s => s.FestId).ToHashSet());

    public Task<SavedFest?> FindSavedAsync(Guid learnerId, string festId, CancellationToken cancellationToken) =>
        Task.FromResult(SavedFests.FirstOrDefault(s => s.LearnerId == learnerId && s.FestId == festId));

    public void Add(SavedFest saved) => SavedFests.Add(saved);

    public void Remove(SavedFest saved) => SavedFests.Remove(saved);

    public Task<FestWaitlistEntry?> FindWaitlistAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken) =>
        Task.FromResult(Waitlist.FirstOrDefault(w => w.LearnerId == learnerId && w.FestId == festId && w.EmployerId == employerId));

    public Task<int> CountWaitlistAsync(string festId, string employerId, DateTime? joinedAt, CancellationToken cancellationToken) =>
        Task.FromResult(Waitlist.Count(w => w.FestId == festId && w.EmployerId == employerId && (joinedAt is null || w.JoinedAt <= joinedAt)));

    public void Add(FestWaitlistEntry entry) => Waitlist.Add(entry);

    public Task<IReadOnlyList<FestOutcome>> ListOutcomesAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FestOutcome>>(Outcomes.Where(o => o.LearnerId == learnerId).ToList());

    public Task<FestOutcome?> FindOutcomeAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken) =>
        Task.FromResult(Outcomes.FirstOrDefault(o => o.LearnerId == learnerId && o.FestId == festId && o.EmployerId == employerId));

    public void Add(FestOutcome outcome) => Outcomes.Add(outcome);

    public void Remove(FestOutcome outcome) => Outcomes.Remove(outcome);

    public Task<IReadOnlyList<FestRating>> ListRatingsAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FestRating>>(Ratings.Where(r => r.LearnerId == learnerId).ToList());

    public Task<FestRating?> FindRatingAsync(Guid learnerId, string festId, CancellationToken cancellationToken) =>
        Task.FromResult(Ratings.FirstOrDefault(r => r.LearnerId == learnerId && r.FestId == festId));

    public void Add(FestRating rating) => Ratings.Add(rating);

    // ---- Match states ----
    Task<IReadOnlyDictionary<string, string>> IMatchStateRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(MatchStates.Where(m => m.LearnerId == learnerId).ToDictionary(m => m.MatchId, m => m.Status));

    Task<MatchState?> IMatchStateRepository.FindAsync(Guid learnerId, string matchId, CancellationToken cancellationToken) =>
        Task.FromResult(MatchStates.FirstOrDefault(m => m.LearnerId == learnerId && m.MatchId == matchId));

    public void Add(MatchState state) => MatchStates.Add(state);

    // ---- Erasure, unit of work, outbox, blobs ----
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        Learners.RemoveAll(x => x.Id == learnerId);
        SavedJobs.RemoveAll(x => x.LearnerId == learnerId);
        Applications.RemoveAll(x => x.LearnerId == learnerId);
        CvFiles.RemoveAll(x => x.LearnerId == learnerId);
        Invitations.RemoveAll(x => x.LearnerId == learnerId);
        Bookings.RemoveAll(x => x.LearnerId == learnerId);
        Waitlist.RemoveAll(x => x.LearnerId == learnerId);
        Outcomes.RemoveAll(x => x.LearnerId == learnerId);
        Ratings.RemoveAll(x => x.LearnerId == learnerId);
        SavedFests.RemoveAll(x => x.LearnerId == learnerId);
        MatchStates.RemoveAll(x => x.LearnerId == learnerId);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Blobs[name] = buffer.ToArray();
    }

    public Task<StoredBlob?> OpenReadAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.TryGetValue(name, out var bytes) ? new StoredBlob(new MemoryStream(bytes), "application/octet-stream") : null);

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        Blobs.Remove(name);
        return Task.CompletedTask;
    }
}

/// <summary>A Learner service whose answers the test sets.</summary>
public sealed class FakeLearnerDirectory : ILearnerDirectory
{
    public ProgressLookup Progress { get; set; } = ProgressLookup.Missing;

    public LearnerCareerProfile? Profile { get; set; }

    public static ProgressLookup Percent(int percent) =>
        new(LookupStatus.Found, new ProfileProgress(percent, percent * 18 / 100, 18, [new QuestFill(2, "hit-me-up", "Hit Me Up", 2, 3, [new QuestStepGap("backup", "Backup contact", 2)])]));

    public Task<ProgressLookup> ProgressAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Progress);

    public Task<LearnerCareerProfile?> CareerProfileAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Profile);
}
