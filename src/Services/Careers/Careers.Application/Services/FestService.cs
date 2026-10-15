using System.Globalization;
using System.Security.Cryptography;
using Careers.Application.Abstractions;
using Careers.Application.Catalogue;
using Careers.Application.Contracts;
using Careers.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Careers.Application.Services;

/// <summary>API-041–056: Hire Fest v2 (Figma 315:44812). Fests from <see cref="IFestCatalogue"/>; tickets in MySQL.</summary>
public interface IFestService
{
    Task<FestSummaryDto> SummaryAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FestDto>> FestsAsync(Guid learnerId, string filter, CancellationToken cancellationToken);

    Task<Result<FestDto>> FestAsync(Guid learnerId, string festId, CancellationToken cancellationToken);

    Task<Result<FestDto>> SaveAsync(Guid learnerId, string festId, bool saved, CancellationToken cancellationToken);

    Task<Result<SlotGridDto>> SlotsAsync(string festId, IReadOnlyList<string> employerIds, CancellationToken cancellationToken);

    /// <returns>The booking and whether it was created (201) rather than changed (200).</returns>
    Task<Result<(BookingDto Booking, bool Created)>> BookAsync(Guid learnerId, string festId, BookRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingDto>> BookingsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result<BookingDto>> BookingAsync(Guid learnerId, string bookingId, CancellationToken cancellationToken);

    /// <returns>The booking left, or null when the whole booking was cancelled.</returns>
    Task<Result<BookingDto?>> CancelAsync(Guid learnerId, string bookingId, string? employerId, CancellationToken cancellationToken);

    Task<Result<WaitlistInfoDto>> WaitlistInfoAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken);

    Task<Result<WaitlistInfoDto>> JoinWaitlistAsync(Guid learnerId, string festId, WaitlistRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PastFestDto>> PastAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result<PastFestDto>> OutcomeAsync(Guid learnerId, string festId, string employerId, string? outcome, CancellationToken cancellationToken);

    Task<Result<PastFestDto>> RateAsync(Guid learnerId, string festId, int stars, CancellationToken cancellationToken);

    Task<FestAlertsDto> AlertsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<FestAlertsDto> SetAlertsAsync(Guid learnerId, FestAlertsDto alerts, CancellationToken cancellationToken);
}

public sealed partial class FestService(
    IFestCatalogue catalogue,
    IJobCatalogue jobs,
    FestViews views,
    CareerStateLoader states,
    IFestBookingRepository bookings,
    IFestActivityRepository activity,
    IApplicationRepository applications,
    ILearnerDirectory directory,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    CareerClock clock,
    IOptions<CareersOptions> options,
    ILogger<FestService> logger) : IFestService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private CareersOptions Rules => options.Value;

    public async Task<FestSummaryDto> SummaryAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var progressTask = directory.ProgressAsync(learnerId, cancellationToken);
        var profile = await directory.CareerProfileAsync(learnerId, cancellationToken);
        var progress = (await progressTask).Progress;
        var mine = await BookedFestsAsync(learnerId, cancellationToken);
        return new FestSummaryDto(
            catalogue.Fests.Count(f => !views.IsEnded(f)),
            mine.Count(b => !views.IsEnded(b.Fest)),
            mine.Count(b => views.IsEnded(b.Fest)),
            progress.Percent,
            Rules.FestBookingGate,
            progress.WithGaps.Select(q => new FestMissingDto(
                $"{q.Name} · {string.Join(", ", q.Left.Select(l => l.Label))}",
                q.Left.Count == 1 ? "1 step left" : $"{q.Left.Count} steps left",
                0,
                $"/quests/{q.Slug}")).ToList(),
            profile?.City ?? string.Empty);
    }

    public async Task<IReadOnlyList<FestDto>> FestsAsync(Guid learnerId, string filter, CancellationToken cancellationToken)
    {
        var city = filter == "city" ? (await directory.CareerProfileAsync(learnerId, cancellationToken))?.City : null;
        var upcoming = catalogue.Fests.Where(f => !views.IsEnded(f)).Where(f => filter switch
        {
            "free" => f.Free,
            "in-person" => !f.Online,
            "online" => f.Online,
            "city" => city is not null && f.City.Equals(city, StringComparison.OrdinalIgnoreCase),
            _ => true,
        }).ToList();

        // Recommended order is the catalogue's (profile match, distance, date), as drawn in H1.
        return await FestDtosAsync(learnerId, upcoming, cancellationToken);
    }

    public async Task<Result<FestDto>> FestAsync(Guid learnerId, string festId, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest)
        {
            return CareersErrors.FestNotFound;
        }

        return (await FestDtosAsync(learnerId, [fest], cancellationToken))[0];
    }

    public async Task<Result<FestDto>> SaveAsync(Guid learnerId, string festId, bool saved, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest)
        {
            return CareersErrors.FestNotFound;
        }

        var existing = await activity.FindSavedAsync(learnerId, festId, cancellationToken);
        if (saved && existing is null)
        {
            activity.Add(SavedFest.Save(tenant.TenantId, learnerId, festId, clock.UtcNow));
        }
        else if (!saved && existing is not null)
        {
            activity.Remove(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await FestDtosAsync(learnerId, [fest], cancellationToken))[0];
    }

    public async Task<Result<SlotGridDto>> SlotsAsync(string festId, IReadOnlyList<string> employerIds, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest)
        {
            return CareersErrors.FestNotFound;
        }

        var known = employerIds.Distinct(StringComparer.Ordinal).Where(id => fest.Employers.Any(e => e.Id == id)).ToList();
        return views.Slots(fest, known, await bookings.SlotUseAsync(festId, cancellationToken));
    }

    public async Task<Result<(BookingDto Booking, bool Created)>> BookAsync(Guid learnerId, string festId, BookRequest request, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest)
        {
            return CareersErrors.FestNotFound;
        }

        if (!views.IsBookable(fest))
        {
            return CareersErrors.BookingClosed;
        }

        // BR-68 / A-230: Job Profile ≥ 60 %. If Learner can't answer, don't guess either way.
        var progress = await directory.ProgressAsync(learnerId, cancellationToken);
        if (progress.Status == LookupStatus.Unavailable)
        {
            return CareersErrors.LearnerUnavailable;
        }

        if (progress.Progress.Percent < Rules.FestBookingGate)
        {
            return CareersErrors.ProfileIncomplete;
        }

        var picks = new List<(FestEmployerSeed Employer, DateOnly Date, string Time)>();
        foreach (var pick in request.Picks.GroupBy(p => p.EmployerId, StringComparer.Ordinal).Select(g => g.Last()))
        {
            if (fest.Employers.FirstOrDefault(e => e.Id == pick.EmployerId) is not { } employer)
            {
                return CareersErrors.EmployerNotFound;
            }

            var date = DateOnly.ParseExact(pick.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (date < fest.Start || date > fest.End || catalogue.Slots(festId, employer.Id, date).All(s => s.Time != pick.Time))
            {
                return CareersErrors.SlotNotFound;
            }

            picks.Add((employer, date, pick.Time));
        }

        var existing = await bookings.FindByFestAsync(learnerId, festId, cancellationToken);
        var kept = existing?.Picks.Where(p => picks.All(n => n.Employer.Id != p.EmployerId)).Select(p => (p.EmployerId, p.Date, p.Time)).ToList() ?? [];
        var final = kept.Concat(picks.Select(p => (EmployerId: p.Employer.Id, p.Date, p.Time))).ToList();
        if (final.Count > Rules.Fest.MaxEmployers)
        {
            return CareersErrors.TooManyEmployers;
        }

        // BR-69: a time overlapping or within the buffer of the other interview is blocked.
        var gap = Rules.Fest.InterviewMinutes + Rules.Fest.BufferMinutes;
        for (var i = 0; i < final.Count; i++)
        {
            for (var k = i + 1; k < final.Count; k++)
            {
                if (final[i].Date == final[k].Date
                    && Math.Abs((FestViews.ParseTime(final[i].Time).ToTimeSpan() - FestViews.ParseTime(final[k].Time).ToTimeSpan()).TotalMinutes) < gap)
                {
                    return CareersErrors.SlotOverlap;
                }
            }
        }

        var usage = await bookings.UsageAsync([festId], cancellationToken);
        if (existing is null && views.SeatsLeft(fest, usage) == 0)
        {
            return CareersErrors.FestFull;
        }

        var used = await bookings.SlotUseAsync(festId, cancellationToken);
        foreach (var (employer, date, time) in picks)
        {
            var mine = existing?.Picks.FirstOrDefault(p => p.EmployerId == employer.Id);
            var holdsThisSlot = mine is not null && mine.Date == date && mine.Time == time;
            var seats = catalogue.Slots(festId, employer.Id, date).First(s => s.Time == time).Seats;
            var taken = used.GetValueOrDefault((employer.Id, date, time)) - (holdsThisSlot ? 1 : 0);
            var employerFull = mine is null && views.EmployerSlotsLeft(fest, employer, usage) == 0;
            if (seats - taken <= 0 || employerFull)
            {
                return CareersErrors.SlotFull;
            }
        }

        var now = clock.UtcNow;
        var booking = existing;
        var created = booking is null;
        if (booking is null)
        {
            var template = request.CvTemplate.Trim();
            booking = FestBooking.Create(tenant.TenantId, learnerId, festId, NewCode(), template.Length > 0 ? $"Job Profile CV · {template}" : "Job Profile CV", now);
            bookings.Add(booking);
            outbox.Add(new FestBooked(learnerId, booking.Id, festId) { TenantId = tenant.TenantId });
        }

        foreach (var (employer, date, time) in picks)
        {
            booking.Pick(employer.Id, date, time, now);
        }

        booking.SetReminders(request.Reminders.Whatsapp, request.Reminders.Sms, request.Reminders.Calendar, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogBooked(logger, learnerId, festId, created);

        var saved = (await activity.ListSavedAsync(learnerId, cancellationToken)).Contains(festId);
        return (views.Booking(booking, fest, await bookings.UsageAsync([festId], cancellationToken), saved), created);
    }

    public async Task<IReadOnlyList<BookingDto>> BookingsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var mine = (await BookedFestsAsync(learnerId, cancellationToken)).Where(b => !views.IsEnded(b.Fest)).ToList();
        var usage = await bookings.UsageAsync(mine.Select(b => b.Fest.Id).ToList(), cancellationToken);
        var saved = await activity.ListSavedAsync(learnerId, cancellationToken);
        return mine
            .OrderBy(b => b.Booking.Picks.Count > 0 ? b.Booking.Picks.Min(p => p.Date) : b.Fest.Start)
            .Select(b => views.Booking(b.Booking, b.Fest, usage, saved.Contains(b.Fest.Id)))
            .ToList();
    }

    public async Task<Result<BookingDto>> BookingAsync(Guid learnerId, string bookingId, CancellationToken cancellationToken)
    {
        var booking = Guid.TryParse(bookingId, out var id) ? await bookings.GetAsync(learnerId, id, cancellationToken) : null;
        if (booking is null || catalogue.Find(booking.FestId) is not { } fest)
        {
            return CareersErrors.BookingNotFound;
        }

        var usage = await bookings.UsageAsync([fest.Id], cancellationToken);
        var saved = (await activity.ListSavedAsync(learnerId, cancellationToken)).Contains(fest.Id);
        return views.Booking(booking, fest, usage, saved);
    }

    public async Task<Result<BookingDto?>> CancelAsync(Guid learnerId, string bookingId, string? employerId, CancellationToken cancellationToken)
    {
        var booking = Guid.TryParse(bookingId, out var id) ? await bookings.GetAsync(learnerId, id, cancellationToken) : null;
        if (booking is null || catalogue.Find(booking.FestId) is not { } fest)
        {
            return CareersErrors.BookingNotFound;
        }

        var affected = string.IsNullOrEmpty(employerId) ? booking.Picks.ToList() : booking.Picks.Where(p => p.EmployerId == employerId).ToList();
        if (!string.IsNullOrEmpty(employerId) && affected.Count == 0)
        {
            return CareersErrors.InterviewNotFound;
        }

        var now = clock.UtcNow;
        var cutoff = TimeSpan.FromHours(Rules.CancelCutoffHours);
        if (affected.Count > 0 && affected.Min(p => clock.ToUtc(p.Date, FestViews.ParseTime(p.Time))) - cutoff < now)
        {
            return CareersErrors.TooLateToCancel;
        }

        var whole = string.IsNullOrEmpty(employerId) || (booking.Unpick(employerId, now) && booking.Picks.Count == 0);
        if (whole)
        {
            bookings.Remove(booking);
            outbox.Add(new FestCancelled(learnerId, booking.Id) { TenantId = tenant.TenantId });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (whole)
        {
            return Result<BookingDto?>.Success(null);
        }

        var usage = await bookings.UsageAsync([fest.Id], cancellationToken);
        var saved = (await activity.ListSavedAsync(learnerId, cancellationToken)).Contains(fest.Id);
        return views.Booking(booking, fest, usage, saved);
    }

    public async Task<Result<WaitlistInfoDto>> WaitlistInfoAsync(Guid learnerId, string festId, string employerId, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest)
        {
            return CareersErrors.FestNotFound;
        }

        if (fest.Employers.All(e => e.Id != employerId))
        {
            return CareersErrors.EmployerNotFound;
        }

        var entry = await activity.FindWaitlistAsync(learnerId, festId, employerId, cancellationToken);
        var position = entry is null
            ? await activity.CountWaitlistAsync(festId, employerId, null, cancellationToken) + 1
            : await activity.CountWaitlistAsync(festId, employerId, entry.JoinedAt, cancellationToken);
        return new WaitlistInfoDto(position, Rules.WaitlistOdds);
    }

    public async Task<Result<WaitlistInfoDto>> JoinWaitlistAsync(Guid learnerId, string festId, WaitlistRequest request, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest)
        {
            return CareersErrors.FestNotFound;
        }

        if (fest.Employers.All(e => e.Id != request.EmployerId))
        {
            return CareersErrors.EmployerNotFound;
        }

        var entry = await activity.FindWaitlistAsync(learnerId, festId, request.EmployerId, cancellationToken);
        if (entry is null)
        {
            entry = FestWaitlistEntry.Join(tenant.TenantId, learnerId, festId, request.EmployerId, request.Auto, request.Whatsapp, clock.UtcNow);
            activity.Add(entry);
        }
        else
        {
            entry.Update(request.Auto, request.Whatsapp);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WaitlistInfoDto(await activity.CountWaitlistAsync(festId, request.EmployerId, entry.JoinedAt, cancellationToken), Rules.WaitlistOdds);
    }

    public async Task<IReadOnlyList<PastFestDto>> PastAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var past = (await BookedFestsAsync(learnerId, cancellationToken)).Where(b => views.IsEnded(b.Fest)).ToList();
        if (past.Count == 0)
        {
            return [];
        }

        var outcomes = await activity.ListOutcomesAsync(learnerId, cancellationToken);
        var ratings = await activity.ListRatingsAsync(learnerId, cancellationToken);
        return past
            .OrderByDescending(b => b.Fest.Start)
            .Select(b => views.Past(b.Booking, b.Fest, outcomes, ratings.FirstOrDefault(r => r.FestId == b.Fest.Id)?.Stars))
            .ToList();
    }

    public async Task<Result<PastFestDto>> OutcomeAsync(Guid learnerId, string festId, string employerId, string? outcome, CancellationToken cancellationToken)
    {
        var attended = await AttendedAsync(learnerId, festId, cancellationToken);
        if (attended is not { } found)
        {
            return CareersErrors.PastFestNotFound;
        }

        var (booking, fest) = found;
        var pick = booking.Picks.FirstOrDefault(p => p.EmployerId == employerId);
        if (pick is null)
        {
            return CareersErrors.InterviewNotFound;
        }

        var now = clock.UtcNow;
        var existing = await activity.FindOutcomeAsync(learnerId, festId, employerId, cancellationToken);
        if (outcome is null)
        {
            if (existing is not null)
            {
                activity.Remove(existing);
            }
        }
        else if (existing is null)
        {
            activity.Add(FestOutcome.Mark(tenant.TenantId, learnerId, festId, employerId, outcome, now));
        }
        else
        {
            existing.Change(outcome, now);
        }

        // BR-70: an offer shows in My Jobs → Applied, once.
        if (outcome == "offer" && fest.Employers.FirstOrDefault(e => e.Id == employerId) is { } employer)
        {
            var key = $"{festId}:{employerId}";
            if (!await applications.FestOfferExistsAsync(learnerId, key, cancellationToken))
            {
                var job = jobs.Jobs.FirstOrDefault(j => j.Company == employer.Company) ?? jobs.Jobs[0];
                var interviewedAt = clock.ToUtc(pick.Date, FestViews.ParseTime(pick.Time));
                applications.Add(JobApplication.FestOffer(tenant.TenantId, learnerId, job.Id, employer.Role, key, booking.CvLabel, interviewedAt, now));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await PastDtoAsync(learnerId, booking, fest, cancellationToken);
    }

    public async Task<Result<PastFestDto>> RateAsync(Guid learnerId, string festId, int stars, CancellationToken cancellationToken)
    {
        if (await AttendedAsync(learnerId, festId, cancellationToken) is not { } found)
        {
            return CareersErrors.PastFestNotFound;
        }

        var existing = await activity.FindRatingAsync(learnerId, festId, cancellationToken);
        if (existing is null)
        {
            activity.Add(FestRating.Rate(tenant.TenantId, learnerId, festId, stars, clock.UtcNow));
        }
        else
        {
            existing.Change(stars, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await PastDtoAsync(learnerId, found.Booking, found.Fest, cancellationToken);
    }

    public async Task<FestAlertsDto> AlertsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await states.LoadAsync(learnerId, withProfile: false, cancellationToken);
        return new FestAlertsDto(state.Learner?.FestAlertsCity ?? false, state.Learner?.FestAlertsOnline ?? false);
    }

    public async Task<FestAlertsDto> SetAlertsAsync(Guid learnerId, FestAlertsDto alerts, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(learnerId, cancellationToken);
        learner.SetFestAlerts(alerts.City, alerts.Online, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FestAlertsDto(learner.FestAlertsCity, learner.FestAlertsOnline);
    }

    private async Task<IReadOnlyList<FestDto>> FestDtosAsync(Guid learnerId, IReadOnlyList<CatalogueFest> fests, CancellationToken cancellationToken)
    {
        if (fests.Count == 0)
        {
            return [];
        }

        var usage = await bookings.UsageAsync(fests.Select(f => f.Id).ToList(), cancellationToken);
        var booked = (await bookings.ListAsync(learnerId, cancellationToken)).Select(b => b.FestId).ToHashSet(StringComparer.Ordinal);
        var saved = await activity.ListSavedAsync(learnerId, cancellationToken);
        return fests.Select(f => views.Fest(f, usage, booked.Contains(f.Id), saved.Contains(f.Id))).ToList();
    }

    private async Task<List<(FestBooking Booking, CatalogueFest Fest)>> BookedFestsAsync(Guid learnerId, CancellationToken cancellationToken) =>
        (await bookings.ListAsync(learnerId, cancellationToken))
            .Select(b => (Booking: b, Fest: catalogue.Find(b.FestId)))
            .Where(x => x.Fest is not null)
            .Select(x => (x.Booking, x.Fest!))
            .ToList();

    /// <summary>The learner's booking for a fest that has ended (only then can outcomes and a rating be given).</summary>
    private async Task<(FestBooking Booking, CatalogueFest Fest)?> AttendedAsync(Guid learnerId, string festId, CancellationToken cancellationToken)
    {
        if (catalogue.Find(festId) is not { } fest || !views.IsEnded(fest))
        {
            return null;
        }

        var booking = await bookings.FindByFestAsync(learnerId, festId, cancellationToken);
        return booking is null ? null : (booking, fest);
    }

    private async Task<PastFestDto> PastDtoAsync(Guid learnerId, FestBooking booking, CatalogueFest fest, CancellationToken cancellationToken)
    {
        var outcomes = await activity.ListOutcomesAsync(learnerId, cancellationToken);
        var rating = await activity.FindRatingAsync(learnerId, fest.Id, cancellationToken);
        return views.Past(booking, fest, outcomes, rating?.Stars);
    }

    /// <summary>"HF-7Q4X-1201": four letters/digits (no look-alikes) and four digits.</summary>
    private static string NewCode()
    {
        var letters = RandomNumberGenerator.GetString(CodeAlphabet, 4);
        return string.Create(CultureInfo.InvariantCulture, $"HF-{letters}-{RandomNumberGenerator.GetInt32(1000, 10000)}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hire Fest booked {LearnerId} {FestId} {Created}")]
    private static partial void LogBooked(ILogger logger, Guid learnerId, string festId, bool created);
}
