using System.Globalization;
using System.Text.Json;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Events;
using Identity.Domain.Accounts;
using Identity.Domain.Claims;
using Identity.Domain.Codes;
using Identity.Domain.Common;
using Identity.Domain.Consents;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>
/// Institute claim links (Figma section 11). <c>GET</c> 404s for unknown links and 410s for claimed / expired /
/// deleted ones; <c>verify</c> signs the learner in. Only the learner who verified can consent, confirm and finish.
/// </summary>
public interface IClaimService
{
    Task<Result<ClaimInviteDto>> GetInviteAsync(string token, Guid? currentAccountId, CancellationToken cancellationToken);

    Task<Result<OtpChallengeDto>> ResendAsync(string token, CancellationToken cancellationToken);

    Task<Result<SignedIn<AuthUserDto>>> VerifyAsync(string token, ClaimCodeRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ClaimSectionDto>>> ConsentAsync(string token, Guid accountId, ClaimConsentRequest request, CancellationToken cancellationToken);

    Task<Result> ConfirmSectionAsync(string token, Guid accountId, string slug, CancellationToken cancellationToken);

    Task<Result<ClaimResultDto>> FinishAsync(string token, Guid accountId, CancellationToken cancellationToken);

    Task<Result> DeclineAsync(string token, ClaimDeclineRequest request, CancellationToken cancellationToken);

    Task<Result<ClaimCreatedDto>> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken);
}

public sealed class ClaimService(
    IClaimInviteRepository invites,
    IAccountRepository accounts,
    IConsentRepository consents,
    ICodeService codes,
    ISessionService sessions,
    ICodeGenerator generator,
    ISecretHasher hasher,
    INotificationSender notifications,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<IdentityServiceOptions> options) : IClaimService
{
    private const string Source = "claim";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private Market Market => Market.For(tenant.Market);

    public async Task<Result<ClaimInviteDto>> GetInviteAsync(string token, Guid? currentAccountId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(token, currentAccountId, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error;
        }

        var invite = loaded.Value;
        var resendIn = 0;
        var triesLeft = options.Value.TriesPerCode;
        if (invite.Stage == ClaimStage.Invited)
        {
            // Opening the link sends the code (V1), once; a reload shows the same challenge.
            var challenge = await codes.IssueAsync(CodePurpose.Claim, invite.CodeTarget, null, reuseUsable: true, cancellationToken);
            if (!challenge.IsSuccess)
            {
                return challenge.Error;
            }

            await SaveAndSendAsync(invite, challenge.Value, cancellationToken);
            resendIn = challenge.Value.ResendIn;
            triesLeft = challenge.Value.TriesLeft;
        }

        var resume = invite.IsClaimant(currentAccountId) && invite.Stage >= ClaimStage.Verified ? Resume(invite) : null;
        return new ClaimInviteDto(
            invite.Institute,
            invite.LearnerName,
            invite.Programme,
            invite.Batch,
            invite.FromCv,
            Market.MaskMobile(invite.Phone),
            resendIn,
            triesLeft,
            invite.DeleteBy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            invite.Certificate,
            resume);
    }

    public async Task<Result<OtpChallengeDto>> ResendAsync(string token, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(token, null, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error;
        }

        var invite = loaded.Value;
        var challenge = await codes.IssueAsync(CodePurpose.Claim, invite.CodeTarget, null, reuseUsable: false, cancellationToken);
        if (!challenge.IsSuccess)
        {
            return challenge.Error;
        }

        await SaveAndSendAsync(invite, challenge.Value, cancellationToken);
        return new OtpChallengeDto(Market.MaskMobile(invite.Phone), challenge.Value.ResendIn, challenge.Value.TriesLeft);
    }

    public async Task<Result<SignedIn<AuthUserDto>>> VerifyAsync(string token, ClaimCodeRequest request, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(token, null, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error;
        }

        var invite = loaded.Value;
        var check = await codes.VerifyAsync(CodePurpose.Claim, invite.CodeTarget, request.Code, cancellationToken);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        var now = Now;
        var account = await accounts.FindByPhoneAsync(invite.Phone, cancellationToken);
        if (account is null)
        {
            var (first, last) = SplitName(invite.LearnerName);
            account = Account.RegisterWithPhone(tenant.TenantId, Market.Code, first, last, invite.Phone, now);
            accounts.Add(account);
            outbox.Add(new LearnerRegistered(account.Id, first, last, null, invite.Phone, Market.Code, Source) { TenantId = tenant.TenantId });
        }

        if (account.IsLocked(now))
        {
            return IdentityErrors.AccountLocked;
        }

        invite.MarkVerified(account.Id, now);
        return await sessions.SignInAsync(account, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ClaimSectionDto>>> ConsentAsync(
        string token,
        Guid accountId,
        ClaimConsentRequest request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadForClaimantAsync(token, accountId, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error;
        }

        var invite = loaded.Value;
        var now = Now;
        invite.Consent(request.ShareProgress, request.VerifyCertificate, request.ApplyForMe, now);

        var policy = options.Value.PrivacyPolicyVersion;
        foreach (var (scope, granted) in new[]
                 {
                     ("claim.share-progress", request.ShareProgress),
                     ("claim.verify-certificate", request.VerifyCertificate),
                     ("claim.apply-for-me", request.ApplyForMe),
                 })
        {
            consents.Add(ConsentRecord.Record(tenant.TenantId, accountId, scope, policy, Market.PrivacyLaw, granted, Source, now));
        }

        outbox.Add(new ClaimConsented(invite.Id, accountId, request.ShareProgress, request.VerifyCertificate, request.ApplyForMe)
        {
            TenantId = tenant.TenantId,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<IReadOnlyList<ClaimSectionDto>>.Success(Sections(invite));
    }

    public async Task<Result> ConfirmSectionAsync(string token, Guid accountId, string slug, CancellationToken cancellationToken)
    {
        var loaded = await LoadForClaimantAsync(token, accountId, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error;
        }

        var invite = loaded.Value;
        if (invite.Stage < ClaimStage.Consented)
        {
            return IdentityErrors.ConsentFirst;
        }

        if (!ReadSections(invite).Any(s => s.Slug == slug))
        {
            return IdentityErrors.SectionNotFound;
        }

        invite.ConfirmSection(slug, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ClaimResultDto>> FinishAsync(string token, Guid accountId, CancellationToken cancellationToken)
    {
        var loaded = await LoadForClaimantAsync(token, accountId, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error;
        }

        var invite = loaded.Value;
        if (invite.Stage == ClaimStage.Finished)
        {
            return ReadResult(invite);
        }

        if (invite.Stage < ClaimStage.Consented)
        {
            return IdentityErrors.ConsentFirst;
        }

        if (ReadSections(invite).Any(s => !invite.IsConfirmed(s.Slug)))
        {
            return IdentityErrors.SectionsPending;
        }

        invite.Finish(Now);
        outbox.Add(new ClaimConfirmed(invite.Id, accountId) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReadResult(invite);
    }

    public async Task<Result> DeclineAsync(string token, ClaimDeclineRequest request, CancellationToken cancellationToken)
    {
        var invite = await invites.FindByTokenHashAsync(hasher.Hash(token), cancellationToken);
        if (invite is null)
        {
            return IdentityErrors.ClaimNotFound;
        }

        if (invite.Status == ClaimStatus.Declined)
        {
            return Result.Success();
        }

        if (invite.Status == ClaimStatus.Claimed)
        {
            return IdentityErrors.ClaimGone("claimed");
        }

        invite.Decline(request.Reason, Now);
        outbox.Add(new ClaimDeclined(invite.Id) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ClaimCreatedDto>> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken)
    {
        var phone = Market.NormalizeMobile(request.Phone);
        if (phone is null)
        {
            return IdentityErrors.InvalidPhone;
        }

        var token = generator.NewToken();
        var sections = request.Sections.Select(s => new StoredSection(s.Slug, s.Quest, s.Title, s.Summary, s.Rows)).ToList();
        var invite = ClaimInvite.Create(
            tenant.TenantId,
            hasher.Hash(token),
            request.Institute,
            request.Name,
            request.Programme,
            request.Batch,
            request.FromCv,
            phone,
            request.Certificate,
            request.DeleteBy.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            JsonSerializer.Serialize(sections, Json),
            request.Result is null ? null : JsonSerializer.Serialize(request.Result, Json),
            Now);
        invites.Add(invite);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ClaimCreatedDto(invite.Id, token);
    }

    private async Task<Result<ClaimInvite>> LoadAsync(string token, Guid? currentAccountId, CancellationToken cancellationToken)
    {
        var invite = await invites.FindByTokenHashAsync(hasher.Hash(token), cancellationToken);
        if (invite is null)
        {
            return IdentityErrors.ClaimNotFound;
        }

        if (invite.Status == ClaimStatus.Declined)
        {
            return IdentityErrors.ClaimGone("deleted");
        }

        // The learner who claimed it can reopen the link to see V4 again; anyone else gets 410 claimed.
        if (invite.Status == ClaimStatus.Claimed && !invite.IsClaimant(currentAccountId))
        {
            return IdentityErrors.ClaimGone("claimed");
        }

        return invite.IsExpired(Now) ? IdentityErrors.ClaimGone("expired") : invite;
    }

    private async Task<Result<ClaimInvite>> LoadForClaimantAsync(string token, Guid accountId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(token, accountId, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded;
        }

        return loaded.Value.IsClaimant(accountId) ? loaded : IdentityErrors.NotClaimant;
    }

    private async Task SaveAndSendAsync(ClaimInvite invite, CodeChallenge challenge, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (challenge.CodeToSend is { } code)
        {
            await notifications.SendAsync(
                new CodeNotification(tenant.TenantId, NotificationChannel.Sms, invite.Phone, NotificationTemplates.ClaimCode, code, challenge.ValidForMinutes),
                cancellationToken);
        }
    }

    private static ClaimResumeDto Resume(ClaimInvite invite) => new(
        invite.Stage switch
        {
            ClaimStage.Finished => "confirmed",
            ClaimStage.Consented => "validate",
            _ => "consent",
        },
        invite.Stage >= ClaimStage.Consented ? Sections(invite) : [],
        invite.Stage == ClaimStage.Finished ? ReadResult(invite) : null);

    private static List<ClaimSectionDto> Sections(ClaimInvite invite) => ReadSections(invite)
        .Select(s => new ClaimSectionDto(
            s.Slug,
            s.Quest,
            s.Title,
            s.Summary,
            invite.IsConfirmed(s.Slug) ? "confirmed" : s.Rows.Any(r => !string.IsNullOrEmpty(r.Issue)) ? "fix" : "review",
            s.Rows))
        .ToList();

    private static List<StoredSection> ReadSections(ClaimInvite invite) =>
        JsonSerializer.Deserialize<List<StoredSection>>(invite.SectionsJson, Json) ?? [];

    private static ClaimResultDto ReadResult(ClaimInvite invite) =>
        (invite.ResultJson is null ? null : JsonSerializer.Deserialize<ClaimResultDto>(invite.ResultJson, Json))
        ?? new ClaimResultDto(0, 0, 0, 0, 0, []);

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("there", string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1]),
        };
    }

    /// <summary>One V3 section as the institute's CV gave it; the status is worked out when read.</summary>
    private sealed record StoredSection(string Slug, int Quest, string Title, string Summary, IReadOnlyList<ClaimRowDto> Rows);
}
