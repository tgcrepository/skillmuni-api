using Identity.Application.Events;
using Identity.Application.Handlers;
using Identity.Application.Services;
using Identity.Domain.Accounts;
using Identity.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.UnitTests.Services;

public sealed class PhotoChangedHandlerTests
{
    [Fact]
    public async Task The_learner_photo_becomes_the_auth_user_photo()
    {
        var h = new IdentityHarness();
        var account = Account.RegisterWithPhone("skillmuni-in", "IN", "Yash", "P", "+919876543210", h.Now);
        h.Store.Accounts.Add(account);
        var handler = new PhotoChangedHandler(h.Store, h.Clock, NullLogger<PhotoChangedHandler>.Instance);

        await handler.HandleAsync(new PhotoChanged(account.Id, "/api/v1/profile/photo?v=2") { TenantId = "skillmuni-in" }, CancellationToken.None);

        Assert.Equal("/api/v1/profile/photo?v=2", account.ToAuthUser().PhotoUrl);

        account.Erase(h.Now);
        Assert.Null(account.PhotoUrl);
    }

    [Fact]
    public async Task An_unknown_account_is_ignored()
    {
        var h = new IdentityHarness();
        var handler = new PhotoChangedHandler(h.Store, h.Clock, NullLogger<PhotoChangedHandler>.Instance);

        await handler.HandleAsync(new PhotoChanged(Guid.NewGuid(), "/x") { TenantId = "skillmuni-in" }, CancellationToken.None);

        Assert.Empty(h.Store.Accounts);
    }
}
