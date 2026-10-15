using Identity.Domain.Codes;

namespace Identity.UnitTests.Domain;

public sealed class VerificationCodeTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    private static VerificationCode NewCode() => VerificationCode.Issue(
        "t", CodePurpose.LoginPhone, "+919876543210", "hash", null, Now, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30), tries: 5);

    [Fact]
    public void Fifth_wrong_attempt_exhausts_the_code()
    {
        var code = NewCode();
        for (var expected = 4; expected >= 1; expected--)
        {
            Assert.Equal(CodeCheck.Wrong, code.Check(matches: false, Now));
            Assert.Equal(expected, code.TriesLeft);
        }

        Assert.Equal(CodeCheck.Exhausted, code.Check(matches: false, Now));
        Assert.Equal(CodeCheck.Exhausted, code.Check(matches: true, Now));
    }

    [Fact]
    public void Expired_code_is_rejected_even_when_right()
    {
        var code = NewCode();
        Assert.Equal(CodeCheck.Expired, code.Check(matches: true, Now.AddMinutes(10)));
    }

    [Fact]
    public void Right_code_works_once()
    {
        var code = NewCode();
        Assert.Equal(CodeCheck.Valid, code.Check(matches: true, Now));
        Assert.Equal(CodeCheck.Expired, code.Check(matches: true, Now));
    }

    [Fact]
    public void Resend_unlocks_after_30_seconds()
    {
        var code = NewCode();
        Assert.False(code.CanResend(Now.AddSeconds(29)));
        Assert.Equal(1, code.SecondsUntilResend(Now.AddSeconds(29)));
        Assert.True(code.CanResend(Now.AddSeconds(30)));
    }
}
