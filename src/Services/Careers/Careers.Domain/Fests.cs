namespace Careers.Domain;

/// <summary>
/// One Hire Fest ticket per learner and fest (BR-69): up to two interviews (picks), reminders and the CV label.
/// Cancelling the whole booking deletes it (and un-ticks the Launchpad mission; XP stays, BR-115).
/// </summary>
public sealed class FestBooking : LearnerOwned
{
    private readonly List<FestPick> _picks = [];

    private FestBooking()
    {
    }

    public string FestId { get; private set; } = string.Empty;

    /// <summary>Ticket code shown with the QR, e.g. <c>HF-7Q4X-1201</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public bool RemindWhatsapp { get; private set; }

    public bool RemindSms { get; private set; }

    public bool RemindCalendar { get; private set; }

    public string CvLabel { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<FestPick> Picks => _picks;

    public static FestBooking Create(string tenantId, Guid learnerId, string festId, string code, string cvLabel, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        FestId = festId,
        Code = code,
        CvLabel = cvLabel,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void SetReminders(bool whatsapp, bool sms, bool calendar, DateTime now)
    {
        RemindWhatsapp = whatsapp;
        RemindSms = sms;
        RemindCalendar = calendar;
        UpdatedAt = now;
    }

    /// <summary>Rebooking an employer replaces that employer's time (BR-69).</summary>
    public void Pick(string employerId, DateOnly date, string time, DateTime now)
    {
        if (_picks.FirstOrDefault(p => p.EmployerId == employerId) is { } existing)
        {
            existing.Move(date, time);
        }
        else
        {
            _picks.Add(FestPick.For(this, employerId, date, time));
        }

        UpdatedAt = now;
    }

    /// <returns>True when that employer's interview was removed.</returns>
    public bool Unpick(string employerId, DateTime now)
    {
        var removed = _picks.RemoveAll(p => p.EmployerId == employerId) > 0;
        if (removed)
        {
            UpdatedAt = now;
        }

        return removed;
    }
}

/// <summary>One 30-minute interview slot held by a booking. Counted against the slot's seats.</summary>
public sealed class FestPick : LearnerOwned
{
    private FestPick()
    {
    }

    public Guid BookingId { get; private set; }

    public string FestId { get; private set; } = string.Empty;

    public string EmployerId { get; private set; } = string.Empty;

    public DateOnly Date { get; private set; }

    /// <summary>"10:30", 24 h, fest local time.</summary>
    public string Time { get; private set; } = string.Empty;

    internal void Move(DateOnly date, string time)
    {
        Date = date;
        Time = time;
    }

    internal static FestPick For(FestBooking booking, string employerId, DateOnly date, string time) => new()
    {
        TenantId = booking.TenantId,
        LearnerId = booking.LearnerId,
        BookingId = booking.Id,
        FestId = booking.FestId,
        EmployerId = employerId,
        Date = date,
        Time = time,
    };
}

/// <summary>Waiting for a slot with a full employer (FR-48). Position = order of joining.</summary>
public sealed class FestWaitlistEntry : LearnerOwned
{
    private FestWaitlistEntry()
    {
    }

    public string FestId { get; private set; } = string.Empty;

    public string EmployerId { get; private set; } = string.Empty;

    /// <summary>Book automatically when a slot frees up.</summary>
    public bool AutoBook { get; private set; }

    public bool Whatsapp { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public static FestWaitlistEntry Join(string tenantId, Guid learnerId, string festId, string employerId, bool autoBook, bool whatsapp, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, FestId = festId, EmployerId = employerId, AutoBook = autoBook, Whatsapp = whatsapp, JoinedAt = now };

    public void Update(bool autoBook, bool whatsapp)
    {
        AutoBook = autoBook;
        Whatsapp = whatsapp;
    }
}

/// <summary>After the fest: what came of one interview (FR-51). offer · waiting · no.</summary>
public sealed class FestOutcome : LearnerOwned
{
    private FestOutcome()
    {
    }

    public string FestId { get; private set; } = string.Empty;

    public string EmployerId { get; private set; } = string.Empty;

    public string Outcome { get; private set; } = string.Empty;

    public DateTime UpdatedAt { get; private set; }

    public static FestOutcome Mark(string tenantId, Guid learnerId, string festId, string employerId, string outcome, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, FestId = festId, EmployerId = employerId, Outcome = outcome, UpdatedAt = now };

    public void Change(string outcome, DateTime now)
    {
        Outcome = outcome;
        UpdatedAt = now;
    }
}

/// <summary>The learner's 1–5 star rating of a fest they attended. Earns no XP (BR-139).</summary>
public sealed class FestRating : LearnerOwned
{
    private FestRating()
    {
    }

    public string FestId { get; private set; } = string.Empty;

    public int Stars { get; private set; }

    public DateTime RatedAt { get; private set; }

    public static FestRating Rate(string tenantId, Guid learnerId, string festId, int stars, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, FestId = festId, Stars = stars, RatedAt = now };

    public void Change(int stars, DateTime now)
    {
        Stars = stars;
        RatedAt = now;
    }
}

public sealed class SavedFest : LearnerOwned
{
    private SavedFest()
    {
    }

    public string FestId { get; private set; } = string.Empty;

    public DateTime SavedAt { get; private set; }

    public static SavedFest Save(string tenantId, Guid learnerId, string festId, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, FestId = festId, SavedAt = now };
}

/// <summary>The learner's action on a My Matches card: saved · applied · accepted (no row = recommended).</summary>
public sealed class MatchState : LearnerOwned
{
    private MatchState()
    {
    }

    public string MatchId { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public DateTime UpdatedAt { get; private set; }

    public static MatchState Set(string tenantId, Guid learnerId, string matchId, string status, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, MatchId = matchId, Status = status, UpdatedAt = now };

    public void Change(string status, DateTime now)
    {
        Status = status;
        UpdatedAt = now;
    }
}
