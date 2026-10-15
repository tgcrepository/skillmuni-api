using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Events;
using Learner.UnitTests.Fakes;

namespace Learner.UnitTests.Services;

public sealed class ProgrammeAndFeedbackTests : IAsyncLifetime
{
    private const string Code = "SKM-MUM-26-0417";
    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    public Task InitializeAsync() => _h.Programmes.SaveAsync(
        new SaveProgrammeRequest(Code, "Customer Support Associate", "NSQF Level 4", "Andheri West, Mumbai", new DateOnly(2026, 10, 12), "3 months", "₹5,000 / month", "12", 50, 50, "Class 12 · 50%+ Eng & Maths"),
        _ct);

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProgrammeSubmission Submission(string english = "60", string maths = "55", string marksFrom = "12", BankDetails? bank = null) =>
        new(Code, marksFrom, english, maths, new Dictionary<string, string> { ["Excel"] = "good" }, bank ?? Bank());

    private static BankDetails Bank() => new("Asha Rao", "savings", "50100234567890", "HDFC0000240", null);

    [Fact]
    public async Task Codes_are_matched_case_insensitively()
    {
        var result = await _h.Programmes.CheckAsync(LearnerHarness.NewLearner(), "skm-mum-26-0417", _ct);
        Assert.Equal("Customer Support Associate", result.Value.Name);
        Assert.Equal("2026-10-12", result.Value.BatchStarts);
    }

    [Fact]
    public async Task Five_wrong_codes_then_429_even_for_a_right_one_until_the_cool_down_ends()
    {
        var who = LearnerHarness.NewLearner();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal("programme-not-found", (await _h.Programmes.CheckAsync(who, "WRONG", _ct)).Error!.Code);
        }

        Assert.Equal("too-many-tries", (await _h.Programmes.CheckAsync(who, Code, _ct)).Error!.Code);
        _h.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.True((await _h.Programmes.CheckAsync(who, Code, _ct)).IsSuccess);
    }

    [Fact]
    public async Task A_right_code_resets_the_wrong_tries()
    {
        var who = LearnerHarness.NewLearner();
        for (var i = 0; i < 4; i++)
        {
            await _h.Programmes.CheckAsync(who, "WRONG", _ct);
        }

        await _h.Programmes.CheckAsync(who, Code, _ct);
        await _h.Programmes.CheckAsync(who, "WRONG", _ct);
        Assert.True((await _h.Programmes.CheckAsync(who, Code, _ct)).IsSuccess);
    }

    [Fact]
    public async Task Submitting_encrypts_the_account_and_returns_its_last_two_digits()
    {
        var who = LearnerHarness.NewLearner();
        var result = (await _h.Programmes.SubmitAsync(who, Submission(), _ct)).Value;

        Assert.Equal(100, result.XpEarned);
        Assert.Equal("90", result.AccountTail);
        Assert.Equal(100, result.Progress.Xp);
        var enrolment = Assert.Single(_h.Store.Enrolments);
        Assert.Equal("7890", enrolment.BankAccountLast4);
        Assert.DoesNotContain("50100234567890", enrolment.BankAccountEncrypted, StringComparison.Ordinal);
        Assert.Equal("50100234567890", _h.Encryptor.Decrypt(enrolment.BankAccountEncrypted!));
        Assert.Single(_h.Store.EventsOf<ProgrammeEnrolled>());
    }

    [Theory]
    [InlineData("49", "60", "12")]
    [InlineData("60", "49", "12")]
    [InlineData("80", "80", "10")]
    public async Task Marks_below_the_rule_are_refused(string english, string maths, string marksFrom)
    {
        var result = await _h.Programmes.SubmitAsync(LearnerHarness.NewLearner(), Submission(english, maths, marksFrom), _ct);
        Assert.Equal("marks-too-low", result.Error!.Code);
    }

    [Fact]
    public async Task A_stipend_needs_a_bank_account()
    {
        var submission = Submission() with { Bank = null };
        Assert.Equal("bank-required", (await _h.Programmes.SubmitAsync(LearnerHarness.NewLearner(), submission, _ct)).Error!.Code);
    }

    [Fact]
    public async Task Indian_routing_must_be_an_ifsc()
    {
        var submission = Submission(bank: Bank() with { Routing = "250655" });
        Assert.Equal("invalid-ifsc", (await _h.Programmes.SubmitAsync(LearnerHarness.NewLearner(), submission, _ct)).Error!.Code);
    }

    [Fact]
    public async Task Enrolling_twice_is_a_conflict()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Programmes.SubmitAsync(who, Submission(), _ct);
        Assert.Equal("already-enrolled", (await _h.Programmes.SubmitAsync(who, Submission(), _ct)).Error!.Code);
    }

    [Theory]
    [InlineData("HDFC0000240", true, null)]
    [InlineData("hdfc0000240", true, null)]
    [InlineData("HDFC1000240", false, "invalid-ifsc")]
    [InlineData("HDF0000240", false, "invalid-ifsc")]
    public async Task Ifsc_shape_is_checked_before_the_lookup(string ifsc, bool ok, string? code)
    {
        var result = await _h.Programmes.FindIfscAsync(ifsc, _ct);
        Assert.Equal(ok, result.IsSuccess);
        Assert.Equal(code, result.Error?.Code);
    }

    [Fact]
    public async Task Ifsc_lookup_outcomes_map_to_404_and_503()
    {
        _h.Banks.Next = Lookup<BankBranchInfo>.NotFound();
        Assert.Equal("ifsc-not-found", (await _h.Programmes.FindIfscAsync("HDFC0000240", _ct)).Error!.Code);
        _h.Banks.Next = Lookup<BankBranchInfo>.Unavailable();
        Assert.Equal("bank-lookup-unavailable", (await _h.Programmes.FindIfscAsync("HDFC0000240", _ct)).Error!.Code);
    }

    [Fact]
    public async Task Feedback_earns_five_xp_at_most_once_every_30_days()
    {
        var who = LearnerHarness.NewLearner();
        var request = new FeedbackRequest(5, ["fun"], "Love it", "/quests");

        Assert.Equal(5, (await _h.Feedback.SendAsync(who, request, _ct)).Xp);
        _h.Clock.Advance(TimeSpan.FromDays(29));
        Assert.Equal(0, (await _h.Feedback.SendAsync(who, request, _ct)).Xp);
        _h.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(5, (await _h.Feedback.SendAsync(who, request, _ct)).Xp);

        Assert.Equal(3, _h.Store.Feedback.Count);
        Assert.Equal(new[] { true, false, true }, _h.Store.EventsOf<FeedbackSent>().Select(e => e.EarnsXp));
    }
}
