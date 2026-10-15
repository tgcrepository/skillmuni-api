namespace Cv.Application;

/// <summary>CV rules. Bound from the <c>Cv</c> config section; every value has a working default.</summary>
public sealed class CvServiceOptions
{
    public const string Section = "Cv";

    /// <summary>10 MB, as the dropzone copy says (S2). Photographed pages count together.</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    public int MaxPages { get; set; } = 10;

    /// <summary>Per learner per day; stops the parser being used as a free OCR service.</summary>
    public int UploadsPerDay { get; set; } = 10;

    /// <summary>Document languages the parser reads well; anything else finishes with S5 other-language.</summary>
    public List<string> SupportedLanguages { get; set; } = ["en", "hi"];

    /// <summary>Fewer details than this → S8 little-found.</summary>
    public int LittleFoundBelow { get; set; } = 6;

    /// <summary>Below this average word confidence a photographed CV is S4 blurry-photo.</summary>
    public double MinPhotoConfidence { get; set; } = 0.6;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>A job claimed longer ago than this is assumed lost (worker crashed) and requeued.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxAttempts { get; set; } = 3;

    /// <summary>The original file is deleted this long after parsing; the extract stays until the learner deletes it.</summary>
    public TimeSpan KeepOriginalFor { get; set; } = TimeSpan.FromDays(30);
}
