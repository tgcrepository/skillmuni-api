using Identity.Domain.Codes;
using Identity.UnitTests.Fakes;

namespace Identity.UnitTests.Services;

public sealed class CodeServiceTests
{
    private const string Target = "+919876543210";

    [Fact]
    public async Task Asking_again_inside_the_resend_window_sends_nothing()
    {
        var h = new IdentityHarness();
        var first = await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);
        h.Clock.Advance(TimeSpan.FromSeconds(10));
        var second = await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);

        Assert.Equal("123456", first.Value.CodeToSend);
        Assert.Null(second.Value.CodeToSend);
        Assert.Equal(20, second.Value.ResendIn);
        Assert.Single(h.Store.Codes);
    }

    [Fact]
    public async Task Resend_replaces_the_old_code()
    {
        var h = new IdentityHarness();
        await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        h.Generator.Code = "654321";
        await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);

        var old = await h.Codes.VerifyAsync(CodePurpose.LoginPhone, Target, "123456", CancellationToken.None);
        Assert.Equal("invalid-code", old.Error?.Code);
        Assert.True((await h.Codes.VerifyAsync(CodePurpose.LoginPhone, Target, "654321", CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task At_most_five_codes_an_hour_per_target()
    {
        var h = new IdentityHarness();
        for (var i = 0; i < 5; i++)
        {
            Assert.True((await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None)).IsSuccess);
            h.Clock.Advance(TimeSpan.FromSeconds(31));
        }

        var sixth = await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);
        Assert.Equal("too-many-codes", sixth.Error?.Code);
    }

    [Fact]
    public async Task No_code_issued_reads_as_expired()
    {
        var h = new IdentityHarness();
        var result = await h.Codes.VerifyAsync(CodePurpose.LoginPhone, Target, "123456", CancellationToken.None);
        Assert.Equal("code-expired", result.Error?.Code);
    }

    [Fact]
    public async Task A_used_code_does_not_hold_back_the_next_flow()
    {
        var h = new IdentityHarness();
        await h.Codes.IssueAsync(CodePurpose.RegisterEmail, "asha@example.com", null, reuseUsable: false, CancellationToken.None);
        Assert.True((await h.Codes.VerifyAsync(CodePurpose.RegisterEmail, "asha@example.com", "123456", CancellationToken.None)).IsSuccess);
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        h.Generator.Code = "654321";

        var next = await h.Codes.IssueAsync(CodePurpose.RegisterEmail, "asha@example.com", null, reuseUsable: false, CancellationToken.None);

        Assert.Equal("654321", next.Value.CodeToSend);
        Assert.Equal(30, next.Value.ResendIn);
        Assert.True((await h.Codes.VerifyAsync(CodePurpose.RegisterEmail, "asha@example.com", "654321", CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task A_code_locked_by_the_hidden_limit_still_waits_for_the_resend_window()
    {
        var h = new IdentityHarness();
        await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);
        for (var i = 0; i < 20; i++)
        {
            await h.Codes.VerifyAsync(CodePurpose.LoginPhone, Target, "000001", CancellationToken.None);
        }

        Assert.Equal("too-many-tries", (await h.Codes.VerifyAsync(CodePurpose.LoginPhone, Target, "123456", CancellationToken.None)).Error?.Code);
        var early = await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None);
        Assert.Null(early.Value.CodeToSend);
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.NotNull((await h.Codes.IssueAsync(CodePurpose.LoginPhone, Target, null, reuseUsable: false, CancellationToken.None)).Value.CodeToSend);
    }
}
