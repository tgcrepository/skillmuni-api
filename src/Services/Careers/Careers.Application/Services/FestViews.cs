using System.Globalization;
using Careers.Application.Abstractions;
using Careers.Application.Catalogue;
using Careers.Application.Contracts;
using Careers.Domain;
using Microsoft.Extensions.Options;

namespace Careers.Application.Services;

/// <summary>Fest, booking and past-fest DTOs (fest.api.ts shapes), with seats counted from every learner's bookings.</summary>
public sealed class FestViews(IFestCatalogue catalogue, CareerClock clock, IOptions<CareersOptions> options)
{
    private FestOptions Rules => options.Value.Fest;

    public bool IsEnded(CatalogueFest fest) => clock.Today > fest.End;

    /// <summary>Booking closes when the fest starts (the last day to book is the day before).</summary>
    public bool IsBookable(CatalogueFest fest) => clock.Today < fest.Start;

    public int SeatsLeft(CatalogueFest fest, FestUsage usage) => Math.Max(0, fest.SeatsLeft - usage.BookingsFor(fest.Id));

    public int EmployerSlotsLeft(CatalogueFest fest, FestEmployerSeed employer, FestUsage usage) =>
        Math.Max(0, employer.SlotsLeft - usage.InterviewsFor(fest.Id, employer.Id));

    public FestDto Fest(CatalogueFest fest, FestUsage usage, bool booked, bool saved) => new(
        fest.Id,
        fest.Name,
        fest.Mode,
        fest.City,
        CareerClock.Iso(fest.Start),
        CareerClock.Iso(fest.End),
        fest.DateLabel,
        fest.Hours,
        fest.Venue,
        fest.VenueNote,
        fest.Distance,
        fest.Organiser,
        fest.Employers.Select(e =>
        {
            var left = EmployerSlotsLeft(fest, e, usage);
            return new FestEmployerDto(e.Id, e.Company, e.Initials, e.Role, e.Openings, left, e.Days, e.Match, left == 0);
        }).ToList(),
        fest.EmployerCount,
        fest.RoleCount,
        fest.Price,
        fest.Free,
        SeatsLeft(fest, usage),
        fest.MatchingRoles,
        fest.BookingCloses,
        fest.CancelPolicy,
        fest.BeforeYouGo,
        booked,
        fest.WaitlistOpen,
        saved);

    public BookingDto Booking(FestBooking booking, CatalogueFest fest, FestUsage usage, bool saved)
    {
        var interviews = booking.Picks
            .OrderBy(p => p.Date).ThenBy(p => p.Time, StringComparer.Ordinal)
            .Select(p =>
            {
                var employer = fest.Employers.FirstOrDefault(e => e.Id == p.EmployerId);
                return new BookedInterviewDto(
                    p.EmployerId,
                    employer?.Company ?? p.EmployerId,
                    employer?.Initials ?? "··",
                    employer?.Role ?? string.Empty,
                    CareerClock.Iso(p.Date),
                    DayLabel(p.Date),
                    Clock(p.Time),
                    Range(p.Time),
                    fest.Online ? "Video call" : $"{fest.Hall}, {employer?.Desk ?? "Desk 1"}");
            })
            .ToList();
        var first = booking.Picks.Count > 0 ? booking.Picks.Min(p => p.Date) : fest.Start;
        return new BookingDto(
            booking.Id.ToString(),
            booking.Code,
            Fest(fest, usage, booked: true, saved),
            interviews,
            Math.Max(0, clock.DaysUntil(first)),
            new RemindersDto(booking.RemindWhatsapp, booking.RemindSms, booking.RemindCalendar),
            booking.CvLabel,

            // H12 live queue needs the venue check-in feed (A-230 "live queue source" to confirm).
            null);
    }

    public PastFestDto Past(FestBooking booking, CatalogueFest fest, IReadOnlyList<FestOutcome> outcomes, int? rating) => new(
        fest.Id,
        fest.Name,
        CareerClock.Iso(fest.Start),
        fest.Start.Day.ToString("00", CultureInfo.InvariantCulture),
        fest.Start.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant(),
        fest.Start.Year.ToString(CultureInfo.InvariantCulture),
        booking.Picks
            .OrderBy(p => p.Date).ThenBy(p => p.Time, StringComparer.Ordinal)
            .Select(p =>
            {
                var employer = fest.Employers.FirstOrDefault(e => e.Id == p.EmployerId);
                return new PastInterviewDto(
                    p.EmployerId,
                    employer?.Company ?? p.EmployerId,
                    employer?.Initials ?? "··",
                    employer?.Role ?? string.Empty,
                    outcomes.FirstOrDefault(o => o.FestId == fest.Id && o.EmployerId == p.EmployerId)?.Outcome);
            })
            .ToList(),
        rating);

    public SlotGridDto Slots(CatalogueFest fest, IReadOnlyList<string> employerIds, IReadOnlyDictionary<(string EmployerId, DateOnly Date, string Time), int> used)
    {
        var days = fest.Days().ToList();
        var slots = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SlotDto>>>(StringComparer.Ordinal);
        foreach (var employerId in employerIds)
        {
            slots[employerId] = days.ToDictionary(
                CareerClock.Iso,
                d => (IReadOnlyList<SlotDto>)catalogue.Slots(fest.Id, employerId, d)
                    .Select(s => new SlotDto(s.Time, Clock(s.Time), Math.Max(0, s.Seats - used.GetValueOrDefault((employerId, d, s.Time)))))
                    .ToList(),
                StringComparer.Ordinal);
        }

        var festDays = days.Select(d =>
        {
            var total = slots.Values.Sum(byDay => byDay[CareerClock.Iso(d)].Sum(s => s.Left));
            return new FestDayDto(CareerClock.Iso(d), DayLabel(d), total < Rules.FewLeftBelow);
        }).ToList();
        return new SlotGridDto(festDays, slots, Rules.InterviewMinutes, Rules.BufferMinutes, Rules.MaxEmployers);
    }

    /// <summary>"Tue 1 Dec".</summary>
    public static string DayLabel(DateOnly date) => date.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    /// <summary>"10:30 am".</summary>
    public static string Clock(string time)
    {
        var t = ParseTime(time);
        var hour = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
        return string.Create(CultureInfo.InvariantCulture, $"{hour}:{t.Minute:00} {(t.Hour >= 12 ? "pm" : "am")}");
    }

    /// <summary>"10:30 – 11:00 am" (the interview's length, with the end's am/pm).</summary>
    public string Range(string time)
    {
        var start = ParseTime(time);
        var end = start.AddMinutes(Rules.InterviewMinutes);
        static string Bare(TimeOnly t) => string.Create(CultureInfo.InvariantCulture, $"{(t.Hour % 12 == 0 ? 12 : t.Hour % 12)}:{t.Minute:00}");
        return $"{Bare(start)} – {Bare(end)} {(end.Hour >= 12 ? "pm" : "am")}";
    }

    public static TimeOnly ParseTime(string time) => TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);
}
