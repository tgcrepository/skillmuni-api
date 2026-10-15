using System.Text.Json;
using Cv.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Cv.Application.Services;

/// <summary>Wakes the parse worker as soon as a CV is queued (other replicas pick it up on their next poll).</summary>
public interface ICvParseSignal
{
    void Notify();
}

/// <summary>API-036–039: upload, status, summary, delete. Only the learner's own CVs are visible.</summary>
public interface ICvService
{
    /// <param name="files">One file, or photographed pages in order.</param>
    Task<Result<CvUploadedDto>> UploadAsync(Guid learnerId, string market, IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken);

    Task<Result<ParseStatusDto>> StatusAsync(Guid learnerId, Guid cvId, CancellationToken cancellationToken);

    Task<Result<CvSummaryDto>> SummaryAsync(Guid learnerId, Guid cvId, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid learnerId, Guid cvId, CancellationToken cancellationToken);
}

public sealed partial class CvService(
    ICvRepository documents,
    IBlobStore blobs,
    ICvParseSignal signal,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<CvServiceOptions> options,
    ILogger<CvService> logger) : ICvService
{
    private const int HeadBytes = 16;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<CvUploadedDto>> UploadAsync(Guid learnerId, string market, IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (files.Count == 0 || files.All(f => f.Length == 0))
        {
            return CvErrors.FileMissing;
        }

        if (files.Count > settings.MaxPages)
        {
            return CvErrors.TooManyPages;
        }

        if (files.Sum(f => f.Length) > settings.MaxBytes)
        {
            return CvErrors.TooLarge;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (await documents.CountUploadsSinceAsync(learnerId, now.AddDays(-1), cancellationToken) >= settings.UploadsPerDay)
        {
            return CvErrors.TooManyUploads;
        }

        // Read and type every page first, so nothing is stored for a refused upload.
        var pages = new List<(byte[] Bytes, string ContentType)>();
        foreach (var file in files)
        {
            using var buffer = new MemoryStream();
            await file.Content.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > settings.MaxBytes)
            {
                return CvErrors.TooLarge;
            }

            var bytes = buffer.ToArray();
            var type = CvFiles.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, HeadBytes)), bytes);

            // Several pages are photos of a paper CV; a document comes on its own.
            if (type is null || (files.Count > 1 && !CvFiles.IsPhoto(type)))
            {
                return CvErrors.UnsupportedType;
            }

            pages.Add((bytes, type));
        }

        var cvId = Guid.NewGuid();
        var names = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var name = $"cvs/{learnerId:N}/{cvId:N}/{i + 1}";
            using var content = new MemoryStream(pages[i].Bytes, writable: false);
            await blobs.UploadAsync(name, content, pages[i].ContentType, cancellationToken);
            names.Add(name);
        }

        var document = CvDocument.Upload(
            cvId,
            tenant.TenantId,
            learnerId,
            market,
            CvFiles.SafeName(files[0].FileName),
            pages[0].ContentType,
            pages.Sum(p => (long)p.Bytes.Length),
            JsonSerializer.Serialize(names, Json),
            now);
        documents.Add(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        signal.Notify();
        LogQueued(logger, document.Id, learnerId, pages.Count);
        return new CvUploadedDto(document.Id.ToString());
    }

    public async Task<Result<ParseStatusDto>> StatusAsync(Guid learnerId, Guid cvId, CancellationToken cancellationToken)
    {
        var document = await documents.FindAsync(cvId, learnerId, cancellationToken);
        return document is null ? CvErrors.NotFound : ToStatus(document);
    }

    public async Task<Result<CvSummaryDto>> SummaryAsync(Guid learnerId, Guid cvId, CancellationToken cancellationToken)
    {
        var document = await documents.FindAsync(cvId, learnerId, cancellationToken);
        if (document is null)
        {
            return CvErrors.NotFound;
        }

        if (document.State == ParseState.Failed)
        {
            return CvErrors.ParseFailed;
        }

        if (document.ExtractJson is not { } json)
        {
            return CvErrors.NotReady;
        }

        var extract = JsonSerializer.Deserialize<CvExtract>(json, Json)!;
        return CvSummaryBuilder.Build(extract, document.Market, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<Result> DeleteAsync(Guid learnerId, Guid cvId, CancellationToken cancellationToken)
    {
        var document = await documents.FindAsync(cvId, learnerId, cancellationToken);
        if (document is null)
        {
            return CvErrors.NotFound;
        }

        foreach (var name in JsonSerializer.Deserialize<List<string>>(document.BlobNamesJson, Json) ?? [])
        {
            await blobs.DeleteAsync(name, cancellationToken);
        }

        documents.Remove(document);
        outbox.Add(new CvDeleted(document.Id, learnerId) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    internal static ParseStatusDto ToStatus(CvDocument d) => d.State switch
    {
        ParseState.Done => new("done", 100, CvDocument.StepCount, null, d.Outcome, d.Detail),
        ParseState.Failed => new("failed", d.Percent, d.Step, null, d.Outcome, d.Detail),
        _ => new("parsing", d.Percent, d.Step, d.Quip ?? CvQuips.Opening, null, null),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "CV queued {CvId} {LearnerId} {Pages}")]
    private static partial void LogQueued(ILogger logger, Guid cvId, Guid learnerId, int pages);
}
