using Learner.Application.Contracts;
using Learner.Application.Services;
using Learner.UnitTests.Fakes;

namespace Learner.UnitTests.Services;

public sealed class CareersProfileTests
{
    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private CareersProfileService Service => new(_h.Store, _h.Store);

    private Task SaveAsync(LearnerRef who, string slug, object values) =>
        _h.Quests.SavePartAsync(who, slug, 1, LearnerHarness.Part(values, done: false), _ct);

    [Fact]
    public async Task Unknown_learner_is_404()
    {
        var result = await Service.GetAsync(Guid.NewGuid(), _ct);
        Assert.Equal("learner-not-found", result.Error!.Code);
    }

    [Fact]
    public async Task Profile_is_read_from_the_quests()
    {
        var who = LearnerHarness.NewLearner("Asha");
        await SaveAsync(who, "who-dis", new { firstName = "Asha", lastName = "Rao" });
        await SaveAsync(who, "hit-me-up", new { phone = "9876540697", city = "Pune", region = "Maharashtra" });
        await SaveAsync(who, "pick-your-arc", new { where = new[] { "near", "relocate" } });
        await SaveAsync(who, "your-flex-era", new
        {
            skills = new object[]
            {
                new { id = "data-entry", name = "Data entry", category = "data", level = 3, source = "suggested" },
                new { id = "excel", name = "MS Excel", category = "business", level = 2, source = "search" },
                new { id = "typing", name = "Typing", category = "data", level = 2, source = "custom" },
                new { id = "zoho", name = "Zoho CRM", category = "business", level = 1, source = "suggested" },
            },
            trophies = new object[]
            {
                new { id = "t1", name = "NSDC data capturing", source = "digilocker", verified = true, linkedSkills = new[] { "data-entry" } },
                new { id = "t2", name = "Udemy Excel", source = "manual", verified = false, linkedSkills = new[] { "excel" } },
                new { id = "t3", name = "Zoho basics", source = "manual", verified = false, linkedSkills = new[] { "zoho" } },
            },
        });
        await SaveAsync(who, "work-lore", new { chapters = new[] { new { id = "c1", role = "Data entry operator", company = "Infosys BPM", skills = new[] { "typing" } } } });
        await SaveAsync(who, "drop-the-receipts", new
        {
            receipts = new Dictionary<string, object>
            {
                ["ch-c1"] = new { status = "verified", fileName = "letter.pdf", at = "15 Oct 2026" },
                ["tr-t3"] = new { status = "checking", fileName = "zoho.pdf", at = "15 Oct 2026" },
            },
        });

        var profile = (await Service.GetAsync(who.Id, _ct)).Value;

        Assert.Equal(("Asha Rao", "•••••• 0697", "Pune", "Maharashtra"), (profile.Name, profile.Contact, profile.City, profile.State));
        Assert.True(profile.Relocate);
        Assert.Equal(
            new[] { new CareersSkillDto("Data entry", "verified"), new CareersSkillDto("MS Excel", "declared"), new CareersSkillDto("Typing", "verified"), new CareersSkillDto("Zoho CRM", "declared") },
            profile.Skills);
    }

    [Fact]
    public async Task Missing_answers_are_null_and_the_verified_mobile_wins()
    {
        var who = LearnerHarness.NewLearner("Ravi");
        await SaveAsync(who, "pick-your-arc", new { where = new[] { "near" } });

        var bare = (await Service.GetAsync(who.Id, _ct)).Value;
        Assert.Equal("Ravi", bare.Name);
        Assert.Null(bare.Contact);
        Assert.Null(bare.City);
        Assert.Null(bare.State);
        Assert.False(bare.Relocate);
        Assert.Empty(bare.Skills);

        _h.Learner(who).SetVerifiedContact("+919123456789", null, _h.Clock.GetUtcNow().UtcDateTime);
        await SaveAsync(who, "pick-your-arc", new { where = new[] { "relocate" } });
        var verified = (await Service.GetAsync(who.Id, _ct)).Value;
        Assert.Equal("•••••• 6789", verified.Contact);
        Assert.True(verified.Relocate);
    }

    [Fact]
    public async Task Relocate_is_null_when_pick_your_arc_is_unanswered()
    {
        var who = LearnerHarness.NewLearner();
        await SaveAsync(who, "who-dis", new { firstName = "Asha" });
        Assert.Null((await Service.GetAsync(who.Id, _ct)).Value.Relocate);
    }
}
