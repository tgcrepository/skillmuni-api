using System.Text.Json;
using Cv.Application.Parsing;
using Cv.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Cv.Application.Services;

/// <summary>Muni's live reactions on the parse screen (mock QUIPS), filled in with what was just found.</summary>
public static class CvQuips
{
    public const string Opening = "Opening your file…";
    public const string Reading = "Reading every page…";
    public const string GotNumber = "Got your number. Literally.";
    public const string LookingForContact = "Looking for your contact details…";
    public const string Timeline = "Building your timeline…";
    public const string Private = "Skipping the private stuff, as promised.";

    public static string Education(CvEducation? top, string market) => top?.Level switch
    {
        "class12" => market == "ZA" ? "Matric (NSC)? Respect." : "Class 12? Respect.",
        "graduate" => "A degree? Respect.",
        "postgraduate" => "A master's? Big brain energy.",
        "diploma" => "A diploma? Respect.",
        _ => "Checking your school and courses…",
    };

    public static string Work(CvJob? first) => first is null
        ? Timeline
        : first.Type == "internship"
            ? $"Ooh, a {first.Role.ToLowerInvariant()} internship? Love that for you."
            : $"{first.Role} at {first.Company}? Love that for you.";
}

/// <summary>Runs one claimed parse job. Called by the worker inside a scope whose tenant is the job's.</summary>
public interface ICvParseJob
{
    Task RunAsync(Guid cvId, CancellationToken cancellationToken);
}

/// <summary>
/// Steps 0–5 as the status screen shows them: open · read · contact · education · work · privacy. Failures end in
/// a Figma S-state (S3 locked, S4 blurry, S6 failed with a support reference); soft outcomes (S5, S7, S8, S9) still
/// finish with whatever was found and pre-fill the quests.
/// </summary>
public sealed partial class CvParseJob(
    ICvRepository documents,
    IBlobStore blobs,
    ICvTextReader reader,
    ICvInterpreter interpreter,
    IAccountDirectory accounts,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<CvServiceOptions> options,
    ILogger<CvParseJob> logger) : ICvParseJob
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(Guid cvId, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(cvId, cancellationToken);
        if (document is null || document.IsFinished)
        {
            return;
        }

        var reference = $"CV-{cvId.ToString("N")[..6].ToUpperInvariant()}";
        try
        {
            await ParseAsync(document, reference, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The support reference lets the team find this log line without the learner sharing the file.
            LogFailed(logger, e, cvId, reference);
            document.Fail("parse-failed", reference, Now());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task ParseAsync(CvDocument document, string reference, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await StepAsync(document, 0, CvQuips.Opening, cancellationToken);

        var pages = new List<CvPage>();
        try
        {
            byte[]? first = null;
            foreach (var name in JsonSerializer.Deserialize<List<string>>(document.BlobNamesJson, Json) ?? [])
            {
                var blob = await blobs.OpenReadAsync(name, cancellationToken)
                    ?? throw new InvalidOperationException("A CV page is missing from storage.");
                using var buffer = new MemoryStream();
                await using (blob.Content)
                {
                    await blob.Content.CopyToAsync(buffer, cancellationToken);
                }

                var bytes = buffer.ToArray();
                first ??= bytes;
                pages.Add(new CvPage(new MemoryStream(bytes, writable: false), blob.ContentType));
            }

            if (first is null)
            {
                throw new InvalidOperationException("A CV has no pages.");
            }

            if (document.ContentType == CvFiles.Pdf && CvFiles.IsEncryptedPdf(first))
            {
                document.Fail("locked-pdf", null, Now());
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            await StepAsync(document, 1, CvQuips.Reading, cancellationToken);
            var read = await reader.ReadAsync(pages, document.FileName, cancellationToken);
            var photo = CvFiles.IsPhoto(document.ContentType);
            if (read.Status == TextReadStatus.Unreadable
                || (photo && read.Text is { } t && t.Confidence < settings.MinPhotoConfidence))
            {
                document.Fail(photo ? "blurry-photo" : "parse-failed", photo ? null : reference, Now());
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            if (read.Status == TextReadStatus.Failed || read.Text is null)
            {
                throw new InvalidOperationException("The text reader failed.");
            }

            var extract = interpreter.Interpret(read.Text, document.Market);

            await StepAsync(document, 2, extract.Contact.Phone is null ? CvQuips.LookingForContact : CvQuips.GotNumber, cancellationToken);
            await StepAsync(document, 3, CvQuips.Education(extract.Education.FirstOrDefault(), document.Market), cancellationToken);
            await StepAsync(document, 4, CvQuips.Work(extract.Work.FirstOrDefault()), cancellationToken);
            await StepAsync(document, 5, CvQuips.Private, cancellationToken);

            var taken = extract.Contact.Email is { } email
                && await accounts.EmailBelongsToSomeoneElseAsync(email, document.LearnerId, cancellationToken) == true;
            var (outcome, detail) = Outcome(extract, taken, settings);

            document.Complete(JsonSerializer.Serialize(extract, Json), outcome, detail, Now());
            outbox.Add(new CvParseCompleted(document.Id, document.LearnerId, outcome, QuestPrefill.Build(extract, includeEmail: !taken))
            {
                TenantId = tenant.TenantId,
            });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogParsed(logger, document.Id, outcome ?? "clean", extract.DetailsFound);
        }
        finally
        {
            foreach (var page in pages)
            {
                await page.Content.DisposeAsync();
            }
        }
    }

    /// <summary>Soft outcomes, most important first.</summary>
    internal static (string? Outcome, string? Detail) Outcome(CvExtract extract, bool emailTaken, CvServiceOptions settings)
    {
        var language = extract.DocumentLanguage?.Split('-')[0].ToLowerInvariant();
        if (language is not null && !settings.SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            return ("other-language", LanguageName(language));
        }

        if (emailTaken)
        {
            return ("email-taken", null);
        }

        if (extract.DetailsFound < settings.LittleFoundBelow)
        {
            return ("little-found", extract.DetailsFound.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return extract.Work.Count == 0 ? ("no-work-history", null) : (null, null);
    }

    // ISO 639-1 codes the text reader reports → English names. Images run in globalization-invariant mode,
    // so CultureInfo can't name languages; unknown codes fall back to the code itself.
    private static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["hi"] = "Hindi",
        ["mr"] = "Marathi",
        ["bn"] = "Bengali",
        ["ta"] = "Tamil",
        ["te"] = "Telugu",
        ["kn"] = "Kannada",
        ["ml"] = "Malayalam",
        ["gu"] = "Gujarati",
        ["pa"] = "Punjabi",
        ["ur"] = "Urdu",
        ["or"] = "Odia",
        ["as"] = "Assamese",
        ["af"] = "Afrikaans",
        ["zu"] = "Zulu",
        ["xh"] = "Xhosa",
        ["st"] = "Sesotho",
        ["tn"] = "Setswana",
        ["fr"] = "French",
        ["de"] = "German",
        ["es"] = "Spanish",
    };

    private static string LanguageName(string code) =>
        LanguageNames.TryGetValue(code.Split('-')[0], out var name) ? name : code;

    private async Task StepAsync(CvDocument document, int step, string quip, CancellationToken cancellationToken)
    {
        document.Advance(step, quip, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    [LoggerMessage(Level = LogLevel.Information, Message = "CV parsed {CvId} {Outcome} {DetailsFound}")]
    private static partial void LogParsed(ILogger logger, Guid cvId, string outcome, int detailsFound);

    [LoggerMessage(Level = LogLevel.Error, Message = "CV parse failed {CvId} {Reference}")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid cvId, string reference);
}
