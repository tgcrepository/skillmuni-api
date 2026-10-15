namespace Cv.Domain;

public enum ParseState
{
    Queued = 0,
    Parsing = 1,
    Done = 2,
    Failed = 3,
}

/// <summary>
/// One uploaded CV (a file, or photographed pages sent together) and its parse job. The original file lives in Blob
/// Storage; only the structured extract is kept here, never the raw text (which may hold ID numbers).
/// </summary>
public sealed class CvDocument
{
    /// <summary>Parse steps the status screen walks through (0–5); 6 means finished.</summary>
    public const int StepCount = 6;

    private CvDocument()
    {
    }

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = string.Empty;

    public Guid LearnerId { get; private set; }

    public string Market { get; private set; } = "IN";

    public string FileName { get; private set; } = string.Empty;

    /// <summary>Detected from the bytes, not the browser.</summary>
    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    /// <summary>Blob names, in page order, as JSON.</summary>
    public string BlobNamesJson { get; private set; } = "[]";

    public ParseState State { get; private set; }

    public int Step { get; private set; }

    public string? Quip { get; private set; }

    /// <summary>CvOutcome code (cv.api.ts), or null for a clean parse.</summary>
    public string? Outcome { get; private set; }

    public string? Detail { get; private set; }

    /// <summary>The structured extract (<see cref="CvExtract"/>) as JSON, once parsed.</summary>
    public string? ExtractJson { get; private set; }

    public int Attempts { get; private set; }

    public DateTime? ClaimedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public bool IsFinished => State is ParseState.Done or ParseState.Failed;

    public int Percent => State switch
    {
        ParseState.Done => 100,
        ParseState.Queued => 0,
        _ => (int)Math.Round(Step * 100.0 / StepCount),
    };

    /// <param name="id">Chosen by the caller, because the blob paths already use it.</param>
    public static CvDocument Upload(
        Guid id,
        string tenantId,
        Guid learnerId,
        string market,
        string fileName,
        string contentType,
        long size,
        string blobNamesJson,
        DateTime now) => new()
        {
            Id = id,
            TenantId = tenantId,
            LearnerId = learnerId,
            Market = market,
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            BlobNamesJson = blobNamesJson,
            State = ParseState.Queued,
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>A worker takes the job (the claim itself is an atomic update in the repository).</summary>
    public void Start(DateTime now)
    {
        State = ParseState.Parsing;
        Attempts++;
        ClaimedAt = now;
        UpdatedAt = now;
    }

    public void Advance(int step, string? quip, DateTime now)
    {
        Step = Math.Clamp(step, Step, StepCount - 1);
        Quip = quip;
        UpdatedAt = now;
    }

    public void Complete(string extractJson, string? outcome, string? detail, DateTime now)
    {
        State = ParseState.Done;
        Step = StepCount;
        Quip = null;
        ExtractJson = extractJson;
        Outcome = outcome;
        Detail = detail;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Fail(string outcome, string? detail, DateTime now)
    {
        State = ParseState.Failed;
        Quip = null;
        Outcome = outcome;
        Detail = detail;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>A crashed worker's job goes back to the queue (or fails after too many attempts).</summary>
    public void Requeue(DateTime now)
    {
        State = ParseState.Queued;
        Step = 0;
        Quip = null;
        ClaimedAt = null;
        UpdatedAt = now;
    }
}
