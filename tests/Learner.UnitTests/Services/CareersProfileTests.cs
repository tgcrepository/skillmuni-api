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
                new { id = "data-entry", name = "Data entry", level = 3 },
                new { id = "excel", name = "MS Excel", level = 2 },
                new { id = "typing", name = "Typing", level = 2 },
                new { id = "zoho", name = "Zoho CRM", level = 1 },
            },
            trophies = new object[]
            {
                new { name = "NSDC data capturing", source = "digilocker", skills = new[] { "data-entry" } },
                new { name = "Udemy Excel", skills = new[] { "MS Excel" } },
            },
        });
        await SaveAsync(who, "drop-the-receipts", new { receipts = new object[] { new { skill = "Typing", status = "verified" }, new { skill = "Zoho CRM", status = "checking" } } });

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
        await SaveAsync(who, "pick-your-arc", new { relocate = false, where = new[] { "relocate" } });
        var verified = (await Service.GetAsync(who.Id, _ct)).Value;
        Assert.Equal("•••••• 6789", verified.Contact);
        Assert.False(verified.Relocate);
    }

    [Fact]
    public async Task Relocate_is_null_when_pick_your_arc_is_unanswered()
    {
        var who = LearnerHarness.NewLearner();
        await SaveAsync(who, "who-dis", new { firstName = "Asha" });
        Assert.Null((await Service.GetAsync(who.Id, _ct)).Value.Relocate);
    }
}
