using SkillMuni.Common.Results;

namespace Cv.Application;

/// <summary>Upload refusals use the frontend's CvOutcome codes (S1, S2), so the same screens show.</summary>
public static class CvErrors
{
    public static readonly Error FileMissing = Error.Validation(
        "file-missing",
        "Choose a file",
        new Dictionary<string, string[]> { ["file"] = ["Choose a file."] });

    public static readonly Error UnsupportedType = Error.Validation("unsupported-type", "Upload a PDF, Word (.docx) file or photo (JPG, PNG, HEIC)");

    public static readonly Error TooLarge = Error.Validation("too-large", "That's over 10 MB");

    public static readonly Error TooManyPages = Error.Validation("too-many-pages", "Up to 10 photographed pages");

    public static readonly Error TooManyUploads = Error.RateLimited("too-many-uploads", "That's a lot of CVs today. Try again tomorrow.");

    public static readonly Error NotFound = Error.NotFound("cv-not-found", "CV not found");

    public static readonly Error NotReady = Error.Conflict("cv-not-ready", "Still reading your CV");

    public static readonly Error ParseFailed = Error.Gone("cv-parse-failed", "This CV couldn't be read");
}
