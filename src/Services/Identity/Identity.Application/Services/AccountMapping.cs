using Identity.Application.Contracts;
using Identity.Domain.Accounts;
using Identity.Domain.Common;

namespace Identity.Application.Services;

internal static class AccountMapping
{
    /// <summary>
    /// The signed-in learner as the top bar needs it. The photo belongs to the Learner service; until it publishes
    /// photo changes to Identity, <c>photoUrl</c> is null.
    /// </summary>
    public static AuthUserDto ToAuthUser(this Account account) => new(
        account.Id.ToString(),
        account.FirstName,
        account.Email,
        account.Phone is null ? null : Market.For(account.Market).FormatMobile(account.Phone),
        PhotoUrl: null);

    public static DateTime AsUtc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
