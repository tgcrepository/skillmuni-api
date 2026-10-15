using Identity.Application.Abstractions;
using Identity.Application.Errors;
using Identity.Domain.Codes;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>
/// What the code screen shows. <see cref="CodeToSend"/> is set only when a new code was created and must be
/// delivered; asking again inside the resend window returns the current challenge without sending anything.
/// </summary>
public sealed record CodeChallenge(string? CodeToSend, int ResendIn, int TriesLeft, int ValidForMinutes);

/// <summary>One-time codes for every flow (BR-136): 6 digits, hidden limit of 20 wrong tries then 429 (BR-11), expiry 410, resend after 30 s.</summary>
public interface ICodeService
{
    /// <param name="reuseUsable">Return a still-valid code's challenge instead of sending a new one (page reloads).</param>
    Task<Result<CodeChallenge>> IssueAsync(
        CodePurpose purpose,
        string target,
        Guid? accountId,
        bool reuseUsable,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks a code. On success the code is consumed and the caller saves it with the rest of its change; a wrong
    /// attempt is saved here so the try counter can't be reset by failing the request.
    /// </summary>
    Task<Result> VerifyAsync(CodePurpose purpose, string target, string code, CancellationToken cancellationToken);
}

public sealed class CodeService(
    IVerificationCodeRepository codes,
    ICodeGenerator generator,
    ISecretHasher hasher,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<IdentityServiceOptions> options) : ICodeService
{
    private readonly IdentityServiceOptions _options = options.Value;

    public async Task<Result<CodeChallenge>> IssueAsync(
        CodePurpose purpose,
        string target,
        Guid? accountId,
        bool reuseUsable,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var latest = await codes.FindLatestAsync(purpose, target, cancellationToken);
        // A used code ended its flow (e.g. sign-up, then the account was erased and the email signs up again): the
        // resend window only guards a code still in play, including one locked by the hidden limit.
        if (latest is not null && !latest.IsConsumed && ((reuseUsable && latest.IsUsable(now)) || !latest.CanResend(now)))
        {
            return new CodeChallenge(null, latest.SecondsUntilResend(now), latest.TriesLeft, latest.ValidForMinutes(now));
        }

        var sentLastHour = await codes.CountIssuedSinceAsync(purpose, target, now.AddHours(-1), cancellationToken);
        if (sentLastHour >= _options.MaxCodesPerHour)
        {
            return IdentityErrors.TooManyCodes;
        }

        latest?.Supersede(now);
        var code = generator.NewCode();
        codes.Add(VerificationCode.Issue(
            tenant.TenantId,
            purpose,
            target,
            hasher.Hash(code),
            accountId,
            now,
            _options.CodeLifetime,
            _options.ResendAfter,
            _options.TriesPerCode));

        return new CodeChallenge(
            code,
            (int)_options.ResendAfter.TotalSeconds,
            _options.TriesPerCode,
            (int)_options.CodeLifetime.TotalMinutes);
    }

    public async Task<Result> VerifyAsync(CodePurpose purpose, string target, string code, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var latest = await codes.FindLatestAsync(purpose, target, cancellationToken);
        if (latest is null)
        {
            return IdentityErrors.CodeExpired;
        }

        var outcome = latest.Check(hasher.Matches(code.Trim(), latest.CodeHash), now);
        switch (outcome)
        {
            case CodeCheck.Valid:
                return Result.Success();
            case CodeCheck.Expired:
                return IdentityErrors.CodeExpired;
            case CodeCheck.Exhausted:
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return IdentityErrors.TooManyTries;
            default:
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return IdentityErrors.InvalidCode(latest.TriesLeft);
        }
    }
}
