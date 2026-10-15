using Identity.Application.Contracts;
using Identity.Domain.Accounts;
using Identity.Domain.Common;

namespace Identity.Application.Services;

internal static class AccountMapping
{
    /// <summary>
    /// The signed-in learner as the top bar needs it. The photo belongs to the Learner service, which publishes
    /// <c>learner.photo.changed</c>; Identity keeps the URL (A-277).
    /// </summary>
    public static AuthUserDto ToAuthUser(this Account account) => new(
        account.Id.ToString(),
        account.FirstName,
        account.Email,
        account.Phone is null ? null : Market.For(account.Market).FormatMobile(account.Phone),
        account.PhotoUrl);

    public static DateTime AsUtc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
