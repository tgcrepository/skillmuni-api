using System.Globalization;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Tenancy;

namespace Careers.Application.Services;

/// <summary>"Now" and "today" in the tenant's market time zone (IST / SAST), and the date labels the screens show.</summary>
public sealed class CareerClock(TimeProvider clock, ITenantContext tenant, IOptions<CareersOptions> options)
{
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public TimeZoneInfo Zone => options.Value.TimeZoneFor(tenant.Market);

    public DateOnly Today => LocalDate(UtcNow);

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

    /// <summary>The UTC instant of a local date and time (fest slots).</summary>
    public DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified), Zone);

    /// <summary>A UTC instant with the market's offset, e.g. 2026-10-10T11:00:00+05:30.</summary>
    public DateTimeOffset Local(DateTime utc)
    {
        var at = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeFromUtc(at, Zone), Zone.GetUtcOffset(at));
    }

    /// <summary>Days from today to <paramref name="date"/> (negative in the past).</summary>
    public int DaysUntil(DateOnly date) => date.DayNumber - Today.DayNumber;

    public int DaysSince(DateTime utc) => Today.DayNumber - LocalDate(utc).DayNumber;

    /// <summary>"2 Oct".</summary>
    public static string Short(DateOnly date) => date.ToString("d MMM", CultureInfo.InvariantCulture);

    public string Short(DateTime utc) => Short(LocalDate(utc));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
