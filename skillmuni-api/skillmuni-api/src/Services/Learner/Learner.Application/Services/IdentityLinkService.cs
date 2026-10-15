using System.Globalization;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Errors;
using Learner.Application.Events;
using Learner.Domain;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-064–067: link a government ID through DigiLocker (India, A-272). Only the masked number is kept.</summary>
public interface IIdentityLinkService
{
    Task<LinkedIdDto?> CurrentAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result<AuthorizeUrlDto>> StartAsync(LearnerRef who, StartDigiLockerRequest request, CancellationToken cancellationToken);

    Task<Result<LinkedIdDto>> LinkAsync(LearnerRef who, LinkDigiLockerRequest request, CancellationToken cancellationToken);

    Task<Result> UnlinkAsync(Guid learnerId, CancellationToken cancellationToken);
}

public sealed class IdentityLinkService(
    ILearnerAccessor accessor,
    ILinkedIdentityRepository identities,
    IDigiLockerClient digiLocker,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock) : IIdentityLinkService
{
    public const string DigiLockerMarket = "IN";

    public async Task<LinkedIdDto?> CurrentAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var linked = await identities.FindAsync(learnerId, cancellationToken);
        return linked is null ? null : ToDto(linked);
    }

    public Task<Result<AuthorizeUrlDto>> StartAsync(LearnerRef who, StartDigiLockerRequest request, CancellationToken cancellationToken)
    {
        Result<AuthorizeUrlDto> result = who.Market != DigiLockerMarket
            ? LearnerErrors.MarketNotSupported
            : !digiLocker.IsAvailable
                ? LearnerErrors.DigiLockerUnavailable
                : new AuthorizeUrlDto(digiLocker.AuthorizeUrl(request.Doc, Guid.NewGuid().ToString("N")).ToString());
        return Task.FromResult(result);
    }

    public async Task<Result<LinkedIdDto>> LinkAsync(LearnerRef who, LinkDigiLockerRequest request, CancellationToken cancellationToken)
    {
        if (who.Market != DigiLockerMarket)
        {
            return LearnerErrors.MarketNotSupported;
        }

        if (!digiLocker.IsAvailable)
        {
            return LearnerErrors.DigiLockerUnavailable;
        }

        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var fetched = await digiLocker.FetchDocumentAsync(request.Doc, request.Code, cancellationToken);
        if (fetched.Status == LookupStatus.NotFound)
        {
            return LearnerErrors.DigiLockerRejected;
        }

        if (fetched.Value is not { } document)
        {
            return LearnerErrors.DigiLockerUnavailable;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var linked = await identities.FindAsync(who.Id, cancellationToken);
        if (linked is null)
        {
            linked = LinkedIdentity.Link(tenant.TenantId, who.Id, request.Doc, document.Masked, document.Last4, document.Birthday, document.Name, now);
            identities.Add(linked);
        }
        else
        {
            linked.Relink(request.Doc, document.Masked, document.Last4, document.Birthday, document.Name, now);
        }

        if (document.Birthday is { } birthday)
        {
            learner.SetVerifiedBirthday(birthday, now);
        }

        outbox.Add(new IdentityLinkChanged(who.Id, request.Doc, Linked: true) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(linked);
    }

    public async Task<Result> UnlinkAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        if (await identities.FindAsync(learnerId, cancellationToken) is { } linked)
        {
            identities.Remove(linked);
            outbox.Add(new IdentityLinkChanged(learnerId, linked.Document, Linked: false) { TenantId = tenant.TenantId });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    internal static LinkedIdDto ToDto(LinkedIdentity linked) => new(
        linked.Document,
        linked.Masked,
        linked.Last4,
        linked.Birthday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        linked.VerifiedName,
        DateTime.SpecifyKind(linked.VerifiedAt, DateTimeKind.Utc));
}
