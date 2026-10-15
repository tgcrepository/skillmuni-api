using System.Text.Json;
using Learner.Application;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Learner.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Learner.UnitTests.Fakes;

/// <summary>Every Learner use case wired to in-memory fakes and a controllable clock.</summary>
public sealed class LearnerHarness
{
    public const string Tenant = "skillmuni-in";

    public LearnerHarness()
    {
        var options = MsOptions.Create(Settings);
        Catalog = new QuestCatalog(Settings.QuestCatalog());
        Accessor = new LearnerAccessor(Store, TenantContext, Clock);
        Progress = new ProgressService(Accessor, Clock, options);
        Quests = new QuestService(Accessor, Store, Progress, Catalog, Store, Store, TenantContext, Clock, options, NullLogger<QuestService>.Instance);
        Drafts = new DraftService(Store, Store, TenantContext, Clock, options);
        IdentityLinks = new IdentityLinkService(Accessor, Store, DigiLocker, Store, Store, TenantContext, Clock);
        Programmes = new ProgrammeService(Accessor, Store, Store, Banks, Encryptor, Progress, Store, Store, TenantContext, Clock, options, NullLogger<ProgrammeService>.Instance);
        Feedback = new FeedbackService(Accessor, Store, Store, Store, TenantContext, Clock, options);
        Profile = new ProfileService(Accessor, Store, Store, Blobs, Progress, Store, Store, TenantContext, Clock, options);
        Trophies = new TrophyService(Accessor, Store, Store, Blobs, Links, DigiLocker, Store, TenantContext, Clock, options);
        Skills = new SkillSearchService(Esco);
        Preferences = new PreferencesService(Accessor, Store, Clock);
        ProfileProgress = new ProfileProgressService(Store, Store, Catalog);
    }

    public LearnerServiceOptions Settings { get; } = new();

    public InMemoryLearnerStore Store { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));

    public FakeBlobStore Blobs { get; } = new();

    public FakeEncryptor Encryptor { get; } = new();

    public FakeBankDirectory Banks { get; } = new();

    public FakeEsco Esco { get; } = new();

    public FakeLinkChecker Links { get; } = new();

    public FakeDigiLocker DigiLocker { get; } = new();

    public ITenantContext TenantContext { get; } = new FixedTenant();

    public QuestCatalog Catalog { get; }

    public LearnerAccessor Accessor { get; }

    public ProgressService Progress { get; }

    public QuestService Quests { get; }

    public DraftService Drafts { get; }

    public IdentityLinkService IdentityLinks { get; }

    public ProgrammeService Programmes { get; }

    public FeedbackService Feedback { get; }

    public ProfileService Profile { get; }

    public TrophyService Trophies { get; }

    public SkillSearchService Skills { get; }

    public PreferencesService Preferences { get; }

    public ProfileProgressService ProfileProgress { get; }

    public static LearnerRef NewLearner(string firstName = "Asha", string market = "IN") => new(Guid.NewGuid(), firstName, market);

    public static SavePartRequest Part(object values, bool done = true, bool? xp = null) =>
        new(JsonSerializer.SerializeToElement(values), done, xp, null);

    public LearnerProfile Learner(LearnerRef who) => Store.Learners.Single(l => l.Id == who.Id);

    /// <summary>Finishes quests 1..n the way the screens do: every part, then complete.</summary>
    public async Task FinishQuestsAsync(LearnerRef who, int count)
    {
        foreach (var quest in Catalog.Quests.Take(count))
        {
            for (var part = 1; part <= quest.Parts.Count; part++)
            {
                await Quests.SavePartAsync(who, quest.Slug, part, Part(new { }), CancellationToken.None);
            }

            await Quests.CompleteAsync(who, quest.Slug, CancellationToken.None);
        }
    }

    private sealed class FixedTenant : ITenantContext
    {
        public string TenantId => Tenant;

        public string Market => "IN";
    }
}
