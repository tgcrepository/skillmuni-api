using Cv.Application;

namespace Cv.Infrastructure.Reading;

/// <summary>
/// Development and tests: no OCR service, a sample CV instead. Name the file like the frontend mock to see each
/// outcome: "blurry" (photo) → S4 · "stuck" → S6 · "afrikaans" → S5 · "nowork" → S7 · "little" → S8 · "taken" → S9
/// (with <see cref="SandboxAccountDirectory"/>). Real encrypted PDFs still end in S3. "za" in the name gives the
/// South African sample.
/// </summary>
internal sealed class SandboxCvTextReader : ICvTextReader
{
    private static readonly string[] India =
    [
        "ASHA RAO",
        "Pune, Maharashtra 411038 | +91 98765 40697 | asha.rao@example.com",
        "Profile",
        "Customer-focused graduate looking for a support role.",
        "Education",
        "B.Com, Savitribai Phule Pune University, 2024",
        "Class 12, Abhinav Vidyalaya, 2021",
        "Experience",
        "Customer Support Intern at Teleperformance, Jan 2024 - Jun 2024",
        "Handled 60+ calls a day; part of the voice process team.",
        "Data Entry Operator, Shree Traders (part-time) Mar 2022 - Dec 2023",
        "Skills: Customer service, MS Excel, Data entry, Typing, Zoho CRM, Tally",
        "Languages: Hindi (native), English (fluent), Marathi",
        "Personal details: Date of birth 14/05/2003, Marital status: Single",
    ];

    private static readonly string[] SouthAfrica =
    [
        "Nkosazana Dlamini",
        "Randburg, Gauteng | 071 234 5678 | nkosazanadlamini@gmail.com",
        "Education",
        "Matric (NSC), Randburg High School, 2021",
        "Contact Centre Operations NQF 4 certificate",
        "Work experience",
        "Call Centre Agent (internship) at Teleperformance, Jan 2024 - Jun 2024",
        "Cashier, Shoprite, Mar 2022 - Dec 2023",
        "Skills: Customer service, Active listening, MS Excel, Call centre systems, Data capturing, Conflict resolution, Typing, Teamwork, Scheduling, Syspro",
        "Languages: isiZulu (home language), English (fluent), Sesotho",
        "ID number: 0105150123081",
    ];

    public Task<TextRead> ReadAsync(IReadOnlyList<CvPage> pages, string fileName, CancellationToken cancellationToken)
    {
        var name = fileName.ToLowerInvariant();
        var photo = pages.All(p => p.ContentType.StartsWith("image/", StringComparison.Ordinal));
        if (name.Contains("stuck", StringComparison.Ordinal))
        {
            return Task.FromResult(new TextRead(TextReadStatus.Failed, null));
        }

        if (name.Contains("blurry", StringComparison.Ordinal))
        {
            return Task.FromResult(new TextRead(TextReadStatus.Read, new CvText(["ASHA"], "en", 0.31, pages.Count, photo)));
        }

        IEnumerable<string> lines = name.Contains("za", StringComparison.Ordinal) ? SouthAfrica : India;
        if (name.Contains("nowork", StringComparison.Ordinal))
        {
            lines = lines.Where(l => !l.Contains(" - ", StringComparison.Ordinal));
        }

        if (name.Contains("little", StringComparison.Ordinal))
        {
            // Four details: the name and two contact details.
            lines = [lines.First(), "asha.rao@example.com | +91 98765 40697"];
        }

        if (name.Contains("taken", StringComparison.Ordinal))
        {
            lines = lines.Select(l => l.Replace("asha.rao@example.com", "taken@example.com", StringComparison.Ordinal));
        }

        var locale = name.Contains("afrikaans", StringComparison.Ordinal) ? "af" : "en";
        return Task.FromResult(new TextRead(TextReadStatus.Read, new CvText(lines.ToList(), locale, 0.97, pages.Count, photo)));
    }
}
