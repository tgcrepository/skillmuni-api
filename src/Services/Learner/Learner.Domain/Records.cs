namespace Learner.Domain;

/// <summary>One quest's saved answers for one learner (A-252: answers survive save &amp; exit).</summary>
public sealed class QuestAnswers : Entity
{
    private QuestAnswers()
    {
    }

    public Guid LearnerId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    /// <summary>A JSON object, field → value. Shape is owned by the quest screens.</summary>
    public string AnswersJson { get; private set; } = "{}";

    public DateTime UpdatedAt { get; private set; }

    public static QuestAnswers Start(string tenantId, Guid learnerId, string slug, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        Slug = slug,
        UpdatedAt = now,
    };

    public void Replace(string answersJson, DateTime now)
    {
        AnswersJson = answersJson;
        UpdatedAt = now;
    }
}

/// <summary>Offline-queue autosave (API-034/035): any JSON value under a client key.</summary>
public sealed class Draft : Entity
{
    private Draft()
    {
    }

    public Guid LearnerId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string ValueJson { get; private set; } = "null";

    public DateTime UpdatedAt { get; private set; }

    public static Draft Create(string tenantId, Guid learnerId, string key, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        Key = key,
        UpdatedAt = now,
    };

    public void Save(string valueJson, DateTime now)
    {
        ValueJson = valueJson;
        UpdatedAt = now;
    }
}

/// <summary>
/// A government ID linked through DigiLocker (A-272). Only which document, the issuer-masked number and its last 4
/// characters are kept: never the full number or a copy.
/// </summary>
public sealed class LinkedIdentity : Entity
{
    private LinkedIdentity()
    {
    }

    public Guid LearnerId { get; private set; }

    /// <summary>aadhaar · pan · driving-licence.</summary>
    public string Document { get; private set; } = string.Empty;

    public string Masked { get; private set; } = string.Empty;

    public string Last4 { get; private set; } = string.Empty;

    public DateOnly? Birthday { get; private set; }

    public string? VerifiedName { get; private set; }

    public DateTime VerifiedAt { get; private set; }

    public static LinkedIdentity Link(
        string tenantId,
        Guid learnerId,
        string document,
        string masked,
        string last4,
        DateOnly? birthday,
        string? verifiedName,
        DateTime now)
    {
        var linked = new LinkedIdentity { TenantId = tenantId, LearnerId = learnerId };
        linked.Relink(document, masked, last4, birthday, verifiedName, now);
        return linked;
    }

    /// <summary>Linking another document replaces the previous one (one ID per learner).</summary>
    public void Relink(string document, string masked, string last4, DateOnly? birthday, string? verifiedName, DateTime now)
    {
        if (last4.Length != 4 || !masked.EndsWith(last4, StringComparison.Ordinal) || masked.Length <= last4.Length)
        {
            throw new ArgumentException("A linked ID keeps only the masked number and its last 4 characters.", nameof(masked));
        }

        Document = document;
        Masked = masked;
        Last4 = last4;
        Birthday = birthday;
        VerifiedName = verifiedName;
        VerifiedAt = now;
    }
}

/// <summary>A programme / project unlocked by a code (Figma section 13).</summary>
public sealed class Programme : Entity
{
    private Programme()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Partner { get; private set; } = string.Empty;

    public string Centre { get; private set; } = string.Empty;

    public DateOnly BatchStarts { get; private set; }

    public string Duration { get; private set; } = string.Empty;

    /// <summary>Empty when no stipend is paid (then no bank section).</summary>
    public string Stipend { get; private set; } = string.Empty;

    /// <summary>"10" or "12".</summary>
    public string MarksFrom { get; private set; } = "12";

    public int MinEnglish { get; private set; }

    public int MinMaths { get; private set; }

    public string Eligibility { get; private set; } = string.Empty;

    public bool PaysStipend => !string.IsNullOrWhiteSpace(Stipend);

    public static Programme Create(string tenantId, string code) => new() { TenantId = tenantId, Code = code };

    public void Describe(
        string name,
        string partner,
        string centre,
        DateOnly batchStarts,
        string duration,
        string stipend,
        string marksFrom,
        int minEnglish,
        int minMaths,
        string eligibility)
    {
        Name = name;
        Partner = partner;
        Centre = centre;
        BatchStarts = batchStarts;
        Duration = duration;
        Stipend = stipend;
        MarksFrom = marksFrom;
        MinEnglish = minEnglish;
        MinMaths = minMaths;
        Eligibility = eligibility;
    }
}

/// <summary>
/// A learner's programme sign-up. The bank account number is stored encrypted (stipends are paid into it) and only
/// its last 4 digits are ever shown.
/// </summary>
public sealed class Enrolment : Entity
{
    private Enrolment()
    {
    }

    public Guid LearnerId { get; private set; }

    public string ProgrammeCode { get; private set; } = string.Empty;

    public string MarksFrom { get; private set; } = string.Empty;

    public int English { get; private set; }

    public int Maths { get; private set; }

    public string ProficiencyJson { get; private set; } = "{}";

    public string? BankHolder { get; private set; }

    public string? BankAccountType { get; private set; }

    public string? BankAccountEncrypted { get; private set; }

    public string? BankAccountLast4 { get; private set; }

    /// <summary>IFSC (IN) or universal branch code (ZA).</summary>
    public string? BankRouting { get; private set; }

    public string? Upi { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    public static Enrolment Submit(
        string tenantId,
        Guid learnerId,
        string programmeCode,
        string marksFrom,
        int english,
        int maths,
        string proficiencyJson,
        DateTime now) => new()
        {
            TenantId = tenantId,
            LearnerId = learnerId,
            ProgrammeCode = programmeCode,
            MarksFrom = marksFrom,
            English = english,
            Maths = maths,
            ProficiencyJson = proficiencyJson,
            SubmittedAt = now,
        };

    public void SetBank(string holder, string accountType, string encryptedAccount, string accountLast4, string routing, string? upi)
    {
        BankHolder = holder;
        BankAccountType = accountType;
        BankAccountEncrypted = encryptedAccount;
        BankAccountLast4 = accountLast4;
        BankRouting = routing;
        Upi = upi;
    }
}

/// <summary>In-app feedback (A-274). <see cref="XpAwarded"/> marks the one per 30 days that earns +5 XP.</summary>
public sealed class Feedback : Entity
{
    private Feedback()
    {
    }

    public Guid LearnerId { get; private set; }

    public int Mood { get; private set; }

    public string TagsJson { get; private set; } = "[]";

    public string Comment { get; private set; } = string.Empty;

    public string Screen { get; private set; } = string.Empty;

    public bool XpAwarded { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static Feedback Give(string tenantId, Guid learnerId, int mood, string tagsJson, string comment, string screen, bool xpAwarded, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        Mood = mood,
        TagsJson = tagsJson,
        Comment = comment,
        Screen = screen,
        XpAwarded = xpAwarded,
        CreatedAt = now,
    };
}

/// <summary>A certificate file for Quest 4's trophy shelf, stored in Blob Storage.</summary>
public sealed class TrophyFile : Entity
{
    private TrophyFile()
    {
    }

    public Guid LearnerId { get; private set; }

    public string BlobName { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static TrophyFile Store(string tenantId, Guid learnerId, string blobName, string fileName, string contentType, long size, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        BlobName = blobName,
        FileName = fileName,
        ContentType = contentType,
        Size = size,
        CreatedAt = now,
    };
}
