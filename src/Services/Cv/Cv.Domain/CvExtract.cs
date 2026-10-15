namespace Cv.Domain;

// What the parser read from a CV, already scrubbed of what Skillmuni never keeps (A-245, privacy copy "Skipping the
// private stuff"): ID numbers, photos, marital status, religion, caste, date of birth from the CV text.

public sealed record PersonName(string First, string Last);

public sealed record CvContact(string? Email, string? Phone, string? City, string? Region, string? PostalCode);

public sealed record CvLanguage(string Name, string Fluency, bool Native);

/// <summary><c>Level</c> uses Quest 3's ids: class10, class12, diploma, graduate, postgraduate, certificate.</summary>
public sealed record CvEducation(string Level, string Title, string? Institute, string? Year);

/// <summary><c>Type</c> uses Quest 5's ids: full_time, part_time, internship, freelance, volunteer.</summary>
public sealed record CvJob(string Role, string Company, string? Start, string? End, bool Current, string Type, bool TypeUnsure);

/// <summary>A skill matched to the deck (<c>Id</c>, <c>Category</c>), or a custom one (category <c>custom</c>).</summary>
public sealed record CvSkill(string Id, string Name, string Category, int Level);

public sealed record CvExtract(
    PersonName? Name,
    CvContact Contact,
    IReadOnlyList<CvLanguage> Languages,
    IReadOnlyList<CvEducation> Education,
    IReadOnlyList<CvJob> Work,
    IReadOnlyList<CvSkill> Skills,
    IReadOnlyList<string> Skipped,
    string? DocumentLanguage)
{
    /// <summary>"Details found": every filled field the learner won't have to type.</summary>
    public int DetailsFound =>
        (Name is null ? 0 : 2)
        + new[] { Contact.Email, Contact.Phone, Contact.City ?? Contact.Region, Contact.PostalCode }.Count(v => !string.IsNullOrEmpty(v))
        + Languages.Count
        + Education.Count
        + Work.Count
        + Skills.Count;
}
