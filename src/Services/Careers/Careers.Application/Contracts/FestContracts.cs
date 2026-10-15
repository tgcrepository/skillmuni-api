namespace Careers.Application.Contracts;

// Shapes match fest.api.ts (FRONTEND_TYPES.md), serialised camelCase.

public sealed record FestEmployerDto(string Id, string Company, string Initials, string Role, int Openings, int SlotsLeft, string Days, int Match, bool Full);

public sealed record FestDto(
    string Id,
    string Name,
    string Mode,
    string City,
    string Start,
    string End,
    string DateLabel,
    string Hours,
    string Venue,
    string VenueNote,
    string? Distance,
    string Organiser,
    IReadOnlyList<FestEmployerDto> Employers,
    int EmployerCount,
    int RoleCount,
    string Price,
    bool Free,
    int SeatsLeft,
    int MatchingRoles,
    string BookingCloses,
    string CancelPolicy,
    IReadOnlyList<string> BeforeYouGo,
    bool Booked,
    bool WaitlistOpen,
    bool Saved);

public sealed record FestDayDto(string Date, string Label, bool FewLeft);

public sealed record SlotDto(string Time, string Label, int Left);

public sealed record SlotGridDto(
    IReadOnlyList<FestDayDto> Days,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SlotDto>>> Slots,
    int Minutes,
    int Buffer,
    int MaxEmployers);

public sealed record BookedInterviewDto(string EmployerId, string Company, string Initials, string Role, string Date, string DayLabel, string Time, string Range, string Desk);

public sealed record RemindersDto(bool Whatsapp, bool Sms, bool Calendar);

public sealed record LiveQueueDto(string EmployerId, int Position, int WaitMinutes, int LateBy, double Progress);

public sealed record LiveStateDto(string CheckedInAt, LiveQueueDto Current);

public sealed record BookingDto(
    string Id,
    string Code,
    FestDto Fest,
    IReadOnlyList<BookedInterviewDto> Interviews,
    int DaysToGo,
    RemindersDto Reminders,
    string Cv,
    LiveStateDto? Live);

public sealed record PastInterviewDto(string EmployerId, string Company, string Initials, string Role, string? Outcome);

public sealed record PastFestDto(string Id, string Name, string Date, string Day, string Month, string Year, IReadOnlyList<PastInterviewDto> Interviews, int? Rating);

public sealed record FestMissingDto(string Title, string Detail, int Xp, string Link);

public sealed record FestSummaryDto(int Upcoming, int Bookings, int Past, int ProfilePercent, int UnlockAt, IReadOnlyList<FestMissingDto> Missing, string City);

public sealed record WaitlistInfoDto(int Position, string Odds);

public sealed record FestAlertsDto(bool City, bool Online);

public sealed record BookPickDto(string EmployerId, string Date, string Time);

public sealed record BookRequest(IReadOnlyList<BookPickDto> Picks, RemindersDto Reminders, string CvTemplate);

public sealed record CancelRequest(string? EmployerId, string? Reason);

public sealed record WaitlistRequest(string EmployerId, bool Auto, bool Whatsapp);

/// <summary><c>outcome</c>: offer · waiting · no · null (clears it).</summary>
public sealed record OutcomeRequest(string EmployerId, string? Outcome);

public sealed record RatingRequest(int Stars);
