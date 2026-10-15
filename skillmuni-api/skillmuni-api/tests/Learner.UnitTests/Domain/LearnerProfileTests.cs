using Learner.Application.Services;
using Learner.Domain;

namespace Learner.UnitTests.Domain;

public sealed class LearnerProfileTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);

    private static LearnerProfile NewLearner() => LearnerProfile.Create(Guid.NewGuid(), "skillmuni-in", "IN", " Asha ", " Rao ", Now);

    [Fact]
    public void Create_trims_names()
    {
        var learner = NewLearner();
        Assert.Equal("Asha Rao", learner.FullName);
    }

    [Fact]
    public void Parts_count_only_forward_and_only_in_the_quest_in_progress()
    {
        var learner = NewLearner();

        Assert.True(learner.RecordPartDone(1, 1, Now));
        Assert.False(learner.RecordPartDone(1, 1, Now));
        Assert.False(learner.RecordPartDone(2, 1, Now));
        Assert.True(learner.RecordPartDone(1, 3, Now));
        Assert.False(learner.RecordPartDone(1, 2, Now));
        Assert.Equal(3, learner.PartsDone);
    }

    [Fact]
    public void Completing_a_quest_counts_once_and_resets_parts()
    {
        var learner = NewLearner();
        learner.RecordPartDone(1, 2, Now);

        Assert.True(learner.CompleteQuest(1, Now));
        Assert.False(learner.CompleteQuest(1, Now));
        Assert.Equal(1, learner.QuestsCompleted);
        Assert.Equal(0, learner.PartsDone);
    }

    [Fact]
    public void Five_wrong_programme_codes_lock_the_check_for_the_cool_down()
    {
        var learner = NewLearner();
        for (var i = 0; i < 4; i++)
        {
            learner.RecordWrongProgrammeCode(Now, 5, TimeSpan.FromMinutes(30));
        }

        Assert.False(learner.IsProgrammeLocked(Now));
        learner.RecordWrongProgrammeCode(Now, 5, TimeSpan.FromMinutes(30));
        Assert.True(learner.IsProgrammeLocked(Now.AddMinutes(29)));
        Assert.False(learner.IsProgrammeLocked(Now.AddMinutes(31)));
    }

    [Fact]
    public void Awarded_xp_shows_until_the_window_ends_and_never_below_the_projection()
    {
        var learner = NewLearner();
        learner.RecordAwardedXp(20, Now);
        learner.RecordAwardedXp(50, Now);

        Assert.Equal(70, learner.DisplayXp(Now.AddMinutes(1), ProgressService.EstimateWindow));
        Assert.Equal(0, learner.DisplayXp(Now.AddMinutes(10), ProgressService.EstimateWindow));

        learner.ApplyProgress(90, 1, "Rookie", 0, 400, 1, 0, Now);
        Assert.Equal(90, learner.DisplayXp(Now.AddMinutes(1), ProgressService.EstimateWindow));
    }

    [Fact]
    public void Gamification_totals_never_go_backwards()
    {
        var learner = NewLearner();
        learner.ApplyProgress(120, 1, "Rookie", 0, 400, 3, 2, Now);
        learner.ApplyProgress(100, 1, "Rookie", 0, 400, 2, 1, Now);

        Assert.Equal(120, learner.Xp);
        Assert.Equal(3, learner.Badges);
    }

    [Fact]
    public void Verified_birthday_never_overwrites_one_the_learner_entered()
    {
        var learner = NewLearner();
        learner.EditDetails("[]", false, Gender.Female, new DateOnly(2003, 5, 14), Now);
        learner.SetVerifiedBirthday(new DateOnly(2004, 8, 21), Now);

        Assert.Equal(new DateOnly(2003, 5, 14), learner.DateOfBirth);
    }

    [Theory]
    [InlineData("XXXX XXXX 4821", "4821", true)]
    [InlineData("XXXXXX234F", "234F", true)]
    [InlineData("2345 6789 4821", "482", false)]
    [InlineData("4821", "4821", false)]
    [InlineData("XXXX XXXX 1234", "4821", false)]
    public void A_linked_id_keeps_only_a_masked_number_ending_in_its_last_4(string masked, string last4, bool valid)
    {
        var link = () => LinkedIdentity.Link("t", Guid.NewGuid(), "aadhaar", masked, last4, null, null, Now);
        if (valid)
        {
            Assert.Equal(last4, link().Last4);
        }
        else
        {
            Assert.Throws<ArgumentException>(link);
        }
    }
}
