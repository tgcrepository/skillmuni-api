using System.Text.Json;
using Cv.Domain;

namespace Cv.Application.Services;

/// <summary>
/// Turns an extract into quest answers in the shapes the quest screens store (see the frontend mocks
/// <c>CV_FLEX</c>, <c>CV_LORE</c> and <c>SAMPLE_ANSWERS</c>). Learner merges them without overwriting what the
/// learner already typed; everything read from the CV is marked so the screens can show "Imported from CV".
/// </summary>
public static class QuestPrefill
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyDictionary<string, JsonElement> Build(CvExtract extract, bool includeEmail)
    {
        var quests = new Dictionary<string, object>(StringComparer.Ordinal);

        if (extract.Name is { } name)
        {
            quests["who-dis"] = new { firstName = name.First, lastName = name.Last };
        }

        var contact = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (extract.Contact.Phone is { } phone)
        {
            contact["phone"] = National(phone);
        }

        if (includeEmail && extract.Contact.Email is { } email)
        {
            contact["email"] = email;
        }

        if (extract.Contact.PostalCode is { } postal)
        {
            contact["postal"] = postal;
        }

        if (extract.Contact.City is { } city)
        {
            contact["city"] = city;
        }

        if (extract.Contact.Region is { } region)
        {
            contact["region"] = region;
        }

        if (contact.Count > 0)
        {
            quests["hit-me-up"] = contact;
        }

        if (extract.Education.Count > 0)
        {
            var top = extract.Education[0];
            quests["big-brain-stuff"] = new
            {
                level = top.Level,
                status = "completed",
                degree = top.Title,
                institute = top.Institute ?? string.Empty,
                year = top.Year ?? string.Empty,
                fromCv = true,
            };
        }

        if (extract.Skills.Count > 0 || extract.Languages.Count > 0)
        {
            quests["your-flex-era"] = new
            {
                skills = extract.Skills.Select(s => new { id = s.Id, name = s.Name, category = s.Category, level = s.Level, source = "cv" }),
                languages = extract.Languages.Select(l => new
                {
                    name = l.Name,
                    fluency = l.Fluency,
                    speak = true,
                    read = l.Fluency.Length > 0,
                    write = l.Fluency is "native" or "fluent",
                    motherTongue = l.Native,
                    fromCv = true,
                }),
            };
        }

        if (extract.Work.Count > 0)
        {
            quests["work-lore"] = new
            {
                workTypes = extract.Work.Select(j => j.Type).Distinct(),
                chapters = extract.Work.Select((j, i) => new
                {
                    id = $"cv{i + 1}",
                    type = j.Type,
                    role = j.Role,
                    company = j.Company,
                    start = j.Start ?? string.Empty,
                    end = j.End ?? string.Empty,
                    current = j.Current,
                    mode = "onsite",
                    skills = Array.Empty<string>(),
                    description = string.Empty,
                    fromCv = true,
                    typeUnsure = j.TypeUnsure,
                }),
            };
        }

        return quests.ToDictionary(q => q.Key, q => JsonSerializer.SerializeToElement(q.Value, Json), StringComparer.Ordinal);
    }

    /// <summary>Quest 2 stores the national number: "+919876540697" → "9876540697".</summary>
    private static string National(string e164) =>
        e164.StartsWith("+91", StringComparison.Ordinal) ? e164[3..]
        : e164.StartsWith("+27", StringComparison.Ordinal) ? e164[3..]
        : e164;
}
