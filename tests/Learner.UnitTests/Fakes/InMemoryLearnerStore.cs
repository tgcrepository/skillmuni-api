using Learner.Application.Abstractions;
using Learner.Domain;
using SkillMuni.Common.Messaging;

namespace Learner.UnitTests.Fakes;

/// <summary>In-memory stand-ins for the repositories, so the Application layer runs without a database.</summary>
public sealed class InMemoryLearnerStore :
    ILearnerRepository,
    IQuestAnswersRepository,
    IDraftRepository,
    ILinkedIdentityRepository,
    IProgrammeRepository,
    IEnrolmentRepository,
    IFeedbackRepository,
    ITrophyFileRepository,
    IJobProfileRepository,
    ILearnerDataEraser,
    IUnitOfWork,
    IOutbox
{
    public List<LearnerProfile> Learners { get; } = [];

    public List<QuestAnswers> Answers { get; } = [];

    public List<Draft> Drafts { get; } = [];

    public List<LinkedIdentity> Identities { get; } = [];

    public List<Programme> Programmes { get; } = [];

    public List<Enrolment> Enrolments { get; } = [];

    public List<Feedback> Feedback { get; } = [];

    public List<TrophyFile> Trophies { get; } = [];

    public List<JobProfileSettings> JobProfiles { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public int Saves { get; private set; }

    public IEnumerable<T> EventsOf<T>()
        where T : IntegrationEvent => Events.OfType<T>();

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    // Learners
    public Task<LearnerProfile?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Learners.FirstOrDefault(l => l.Id == learnerId));

    public Task<LearnerProfile> GetOrAddAsync(LearnerProfile candidate, CancellationToken cancellationToken)
    {
        var existing = Learners.FirstOrDefault(l => l.Id == candidate.Id);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        Learners.Add(candidate);
        Saves++;
        return Task.FromResult(candidate);
    }

    public void Add(LearnerProfile learner) => Learners.Add(learner);

    // Quest answers
    public Task<QuestAnswers?> FindAsync(Guid learnerId, string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Answers.FirstOrDefault(a => a.LearnerId == learnerId && a.Slug == slug));

    public Task<IReadOnlyDictionary<string, string>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(Answers.Where(a => a.LearnerId == learnerId).ToDictionary(a => a.Slug, a => a.AnswersJson));

    public void Add(QuestAnswers answers) => Answers.Add(answers);

    public Task<DateTime?> LastUpdatedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Answers.Where(a => a.LearnerId == learnerId).Select(a => (DateTime?)a.UpdatedAt).Max());

    // Job Profile
    Task<JobProfileSettings?> IJobProfileRepository.FindAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(JobProfiles.FirstOrDefault(j => j.LearnerId == learnerId));

    public void Add(JobProfileSettings settings) => JobProfiles.Add(settings);

    // Drafts
    Task<Draft?> IDraftRepository.FindAsync(Guid learnerId, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Drafts.FirstOrDefault(d => d.LearnerId == learnerId && d.Key == key));

    public void Add(Draft draft) => Drafts.Add(draft);

    // Linked identities
    Task<LinkedIdentity?> ILinkedIdentityRepository.FindAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Identities.FirstOrDefault(i => i.LearnerId == learnerId));

    public void Add(LinkedIdentity identity) => Identities.Add(identity);

    public void Remove(LinkedIdentity identity) => Identities.Remove(identity);

    // Programmes and enrolments
    Task<Programme?> IProgrammeRepository.FindAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(Programmes.FirstOrDefault(p => p.Code == code));

    public void Add(Programme programme) => Programmes.Add(programme);

    public Task<bool> ExistsAsync(Guid learnerId, string programmeCode, CancellationToken cancellationToken) =>
        Task.FromResult(Enrolments.Any(e => e.LearnerId == learnerId && e.ProgrammeCode == programmeCode));

    public void Add(Enrolment enrolment) => Enrolments.Add(enrolment);

    // Feedback
    public Task<DateTime?> LastXpAwardAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Feedback.Where(f => f.LearnerId == learnerId && f.XpAwarded).Select(f => (DateTime?)f.CreatedAt).Max());

    public void Add(Feedback feedback) => Feedback.Add(feedback);

    // Trophies
    public Task<int> CountAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Trophies.Count(t => t.LearnerId == learnerId));

    public void Add(TrophyFile file) => Trophies.Add(file);

    // Erasure
    public Task<IReadOnlyList<string>> ListBlobsAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(
            Learners.Where(l => l.Id == learnerId && l.PhotoBlob != null).Select(l => l.PhotoBlob!)
                .Concat(Trophies.Where(t => t.LearnerId == learnerId).Select(t => t.BlobName))
                .Concat(JobProfiles.Where(j => j.LearnerId == learnerId && j.CvBlob != null).Select(j => j.CvBlob!))
                .ToList());

    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        Learners.RemoveAll(l => l.Id == learnerId);
        Answers.RemoveAll(a => a.LearnerId == learnerId);
        Drafts.RemoveAll(d => d.LearnerId == learnerId);
        Identities.RemoveAll(i => i.LearnerId == learnerId);
        Enrolments.RemoveAll(e => e.LearnerId == learnerId);
        Feedback.RemoveAll(f => f.LearnerId == learnerId);
        Trophies.RemoveAll(t => t.LearnerId == learnerId);
        JobProfiles.RemoveAll(j => j.LearnerId == learnerId);
        return Task.CompletedTask;
    }
}
