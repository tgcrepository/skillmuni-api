using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Errors;
using Learner.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-058–060: Quest 4's trophy shelf. A failed link check never blocks saving the trophy.</summary>
public interface ITrophyService
{
    Task<Result<LinkCheckDto>> CheckLinkAsync(LearnerRef who, CheckLinkRequest request, CancellationToken cancellationToken);

    Task<Result<FileIdDto>> UploadAsync(LearnerRef who, UploadedFile? file, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ImportedTrophyDto>>> ImportDigiLockerAsync(LearnerRef who, CancellationToken cancellationToken);
}

public sealed class TrophyService(
    ILearnerAccessor accessor,
    ILinkedIdentityRepository identities,
    ITrophyFileRepository files,
    IBlobStore blobs,
    ILinkChecker links,
    IDigiLockerClient digiLocker,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options) : ITrophyService
{
    /// <summary>A soft cap so the shelf can't be used as free file storage.</summary>
    public const int MaxFiles = 50;

    private static readonly Error TooManyFiles = Error.Validation("too-many-files", $"Up to {MaxFiles} certificate files");

    public async Task<Result<LinkCheckDto>> CheckLinkAsync(LearnerRef who, CheckLinkRequest request, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var names = new List<string>();
        if (learner.FullName.Length > 0)
        {
            names.Add(learner.FullName);
        }

        if ((await identities.FindAsync(who.Id, cancellationToken))?.VerifiedName is { Length: > 0 } verified)
        {
            names.Add(verified);
        }

        if (names.Count == 0)
        {
            return new LinkCheckDto(false);
        }

        var result = await links.PageMentionsAsync(new Uri(request.Url), names, cancellationToken);
        return result.Status switch
        {
            LookupStatus.Found => new LinkCheckDto(result.Value),
            LookupStatus.Timeout => LearnerErrors.LinkTimeout,

            // Not found, blocked (private address) or failing: just "not verified"; the learner can still save it.
            _ => new LinkCheckDto(false),
        };
    }

    public async Task<Result<FileIdDto>> UploadAsync(LearnerRef who, UploadedFile? file, CancellationToken cancellationToken)
    {
        if (await files.CountAsync(who.Id, cancellationToken) >= MaxFiles)
        {
            return TooManyFiles;
        }

        var read = await FileSniffer.ReadAsync(file, options.Value.MaxTrophyFileBytes, FileSniffer.Certificates, cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        await accessor.GetOrCreateAsync(who, cancellationToken);
        var blobName = $"trophies/{who.Id:N}/{Guid.NewGuid():N}";
        await using (read.Value.Content)
        {
            await blobs.UploadAsync(blobName, read.Value.Content, read.Value.ContentType, cancellationToken);
        }

        var trophy = TrophyFile.Store(
            tenant.TenantId,
            who.Id,
            blobName,
            SafeFileName(file!.FileName),
            read.Value.ContentType,
            file.Length,
            clock.GetUtcNow().UtcDateTime);
        files.Add(trophy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FileIdDto(trophy.Id.ToString());
    }

    public async Task<Result<IReadOnlyList<ImportedTrophyDto>>> ImportDigiLockerAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        if (who.Market != IdentityLinkService.DigiLockerMarket)
        {
            return LearnerErrors.MarketNotSupported;
        }

        if (!digiLocker.IsAvailable)
        {
            return LearnerErrors.DigiLockerUnavailable;
        }

        var imported = await digiLocker.ImportCertificatesAsync(who.Id, cancellationToken);
        return imported.Status switch
        {
            LookupStatus.Found when imported.Value is { } list => Result<IReadOnlyList<ImportedTrophyDto>>.Success(list),
            LookupStatus.NotFound => Result<IReadOnlyList<ImportedTrophyDto>>.Success([]),
            _ => LearnerErrors.DigiLockerUnavailable,
        };
    }

    /// <summary>Only the last path segment, without control characters, at most 200 characters.</summary>
    private static string SafeFileName(string name)
    {
        var last = Path.GetFileName(name.Replace('\\', '/'));
        var clean = new string(last.Where(c => !char.IsControl(c)).ToArray());
        return clean.Length switch
        {
            0 => "certificate",
            > 200 => clean[..200],
            _ => clean,
        };
    }
}
