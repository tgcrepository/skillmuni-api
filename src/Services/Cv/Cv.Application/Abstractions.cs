using Cv.Domain;

namespace Cv.Application;

/// <summary>Queries are scoped to the current tenant by the DbContext, except the worker's cross-tenant claim.</summary>
public interface ICvRepository
{
    /// <summary>Only the learner's own CV: someone else's id is "not found".</summary>
    Task<CvDocument?> FindAsync(Guid cvId, Guid learnerId, CancellationToken cancellationToken);

    /// <summary>For the parse job, which already knows the tenant.</summary>
    Task<CvDocument?> GetAsync(Guid cvId, CancellationToken cancellationToken);

    Task<int> CountUploadsSinceAsync(Guid learnerId, DateTime since, CancellationToken cancellationToken);

    Task<IReadOnlyList<CvDocument>> ListForLearnerAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(CvDocument document);

    void Remove(CvDocument document);
}

/// <summary>The worker's view of the queue: atomic claims across tenants.</summary>
public interface ICvJobQueue
{
    /// <summary>Claims the oldest queued (or stale) job, or returns null when there is none.</summary>
    Task<ClaimedJob?> ClaimNextAsync(DateTime now, DateTime staleBefore, int maxAttempts, CancellationToken cancellationToken);

    /// <summary>Finished CVs whose original files are older than <paramref name="before"/>.</summary>
    Task<IReadOnlyList<ExpiredFiles>> ListExpiredFilesAsync(DateTime before, int max, CancellationToken cancellationToken);

    Task ForgetFilesAsync(Guid cvId, CancellationToken cancellationToken);
}

public sealed record ClaimedJob(Guid CvId, string TenantId);

public sealed record ExpiredFiles(Guid CvId, IReadOnlyList<string> BlobNames);

/// <summary>Saves every change in the use case, outbox rows included, in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public enum TextReadStatus
{
    Read,

    /// <summary>The pages hold too little legible text (a blurry or dark photo).</summary>
    Unreadable,

    Failed,
}

/// <summary>Text read from the pages. Lines keep the document's reading order.</summary>
public sealed record CvText(IReadOnlyList<string> Lines, string? Locale, double Confidence, int Pages, bool FromPhoto)
{
    public string Content => string.Join('\n', Lines);
}

public sealed record TextRead(TextReadStatus Status, CvText? Text);

public sealed record CvPage(Stream Content, string ContentType);

/// <summary>OCR / layout reading (Azure AI Document Intelligence "prebuilt-read").</summary>
public interface ICvTextReader
{
    Task<TextRead> ReadAsync(IReadOnlyList<CvPage> pages, string fileName, CancellationToken cancellationToken);
}

/// <summary>Identity: does another account already use this email (S9)?</summary>
public interface IAccountDirectory
{
    /// <returns>Null when the lookup is unavailable; the parse then goes on without the check.</returns>
    Task<bool?> EmailBelongsToSomeoneElseAsync(string email, Guid learnerId, CancellationToken cancellationToken);
}
