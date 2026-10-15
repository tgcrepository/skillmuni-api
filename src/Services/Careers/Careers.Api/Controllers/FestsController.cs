using Asp.Versioning;
using Careers.Application.Contracts;
using Careers.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Careers.Api.Controllers;

/// <summary>API-041–056: Hire Fest.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/fests")]
public sealed class FestsController(IFestService fests) : CareersControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<FestSummaryDto>> Summary(CancellationToken cancellationToken) =>
        await fests.SummaryAsync(LearnerId, cancellationToken);

    /// <summary><c>filter</c>: all · free · in-person · online · city (the learner's city). Upcoming fests only.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FestDto>>> List([FromQuery] string? filter, CancellationToken cancellationToken) =>
        Ok(await fests.FestsAsync(LearnerId, filter ?? "all", cancellationToken));

    [HttpGet("bookings")]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> Bookings(CancellationToken cancellationToken) =>
        Ok(await fests.BookingsAsync(LearnerId, cancellationToken));

    [HttpGet("bookings/{id}")]
    public async Task<IActionResult> Booking(string id, CancellationToken cancellationToken) =>
        (await fests.BookingAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    /// <summary>One interview (<c>employerId</c>) or the whole booking (<c>employerId: null</c>). 204 when nothing is left.</summary>
    [HttpPost("bookings/{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, CancelRequest request, CancellationToken cancellationToken) =>
        (await fests.CancelAsync(LearnerId, id, request.EmployerId, cancellationToken)).ToActionResult(this);

    [HttpGet("past")]
    public async Task<ActionResult<IReadOnlyList<PastFestDto>>> Past(CancellationToken cancellationToken) =>
        Ok(await fests.PastAsync(LearnerId, cancellationToken));

    /// <summary>offer · waiting · no · null. An offer also shows in My Jobs → Applied (BR-70).</summary>
    [HttpPut("past/{id}/outcome")]
    public async Task<IActionResult> Outcome(string id, OutcomeRequest request, CancellationToken cancellationToken) =>
        (await fests.OutcomeAsync(LearnerId, id, request.EmployerId, request.Outcome, cancellationToken)).ToActionResult(this);

    /// <summary>1–5 stars. No XP (BR-139).</summary>
    [HttpPut("past/{id}/rating")]
    public async Task<IActionResult> Rate(string id, RatingRequest request, CancellationToken cancellationToken) =>
        (await fests.RateAsync(LearnerId, id, request.Stars, cancellationToken)).ToActionResult(this);

    [HttpGet("alerts")]
    public async Task<ActionResult<FestAlertsDto>> Alerts(CancellationToken cancellationToken) =>
        await fests.AlertsAsync(LearnerId, cancellationToken);

    [HttpPut("alerts")]
    public async Task<ActionResult<FestAlertsDto>> SetAlerts(FestAlertsDto request, CancellationToken cancellationToken) =>
        await fests.SetAlertsAsync(LearnerId, request, cancellationToken);

    [HttpGet("{id}")]
    public async Task<IActionResult> Fest(string id, CancellationToken cancellationToken) =>
        (await fests.FestAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpPut("{id}/saved")]
    public async Task<IActionResult> Save(string id, SaveRequest request, CancellationToken cancellationToken) =>
        (await fests.SaveAsync(LearnerId, id, request.Saved, cancellationToken)).ToActionResult(this);

    /// <summary><c>employers</c>: comma-separated employer ids → employerId → date → slots.</summary>
    [HttpGet("{id}/slots")]
    public async Task<IActionResult> Slots(string id, [FromQuery] string? employers, CancellationToken cancellationToken) =>
        (await fests.SlotsAsync(id, (employers ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), cancellationToken))
            .ToActionResult(this);

    /// <summary>
    /// 201 for a new ticket, 200 when rescheduling or adding to the existing one. 422 <c>profile-incomplete</c> under 60 %
    /// (BR-68), <c>too-many-employers</c>, <c>slot-overlap</c>; 409 <c>slot-full</c> / <c>fest-full</c>; 410 <c>booking-closed</c>.
    /// </summary>
    [HttpPost("{id}/book")]
    [Idempotent]
    public async Task<IActionResult> Book(string id, BookRequest request, CancellationToken cancellationToken) =>
        (await fests.BookAsync(LearnerId, id, request, cancellationToken)).ToActionResult(this, result => result.Created
            ? Created($"/api/v1/fests/bookings/{result.Booking.Id}", result.Booking)
            : Ok(result.Booking));

    [HttpGet("{id}/waitlist")]
    public async Task<IActionResult> WaitlistInfo(string id, [FromQuery] string? employerId, CancellationToken cancellationToken) =>
        (await fests.WaitlistInfoAsync(LearnerId, id, employerId ?? string.Empty, cancellationToken)).ToActionResult(this);

    [HttpPost("{id}/waitlist")]
    [Idempotent]
    public async Task<IActionResult> JoinWaitlist(string id, WaitlistRequest request, CancellationToken cancellationToken) =>
        (await fests.JoinWaitlistAsync(LearnerId, id, request, cancellationToken)).ToActionResult(this);
}
