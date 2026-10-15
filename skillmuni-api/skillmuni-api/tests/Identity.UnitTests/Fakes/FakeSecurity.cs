using Identity.Application.Abstractions;
using Identity.Domain.Accounts;
using Identity.Domain.Sessions;

namespace Identity.UnitTests.Fakes;

public sealed class FakeHasher : ISecretHasher
{
    public string Hash(string secret) => $"h:{secret}";

    public bool Matches(string secret, string hash) => Hash(secret) == hash;
}

public sealed class FakeCodeGenerator : ICodeGenerator
{
    private int _tokens;

    public string Code { get; set; } = "123456";

    public string NewCode() => Code;

    public string NewToken() => $"token-{++_tokens}";
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"p:{password}";

    public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
}

public sealed class FakeTokenIssuer : ITokenIssuer
{
    public AccessToken Issue(Account account, Session session, DateTime now) =>
        new($"access:{account.Id}:{session.Id}", now.AddMinutes(10));
}

public sealed class FakeNotifications : INotificationSender
{
    public List<CodeNotification> Sent { get; } = [];

    public Task SendAsync(CodeNotification notification, CancellationToken cancellationToken)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

public sealed class FakeGoogle : IGoogleTokenValidator
{
    public Dictionary<string, GoogleIdentity> Tokens { get; } = [];

    public Task<GoogleIdentity?> ValidateAsync(string credential, string clientId, CancellationToken cancellationToken) =>
        Task.FromResult<GoogleIdentity?>(Tokens.TryGetValue(credential, out var identity) ? identity : null);
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? AccountId { get; set; }

    public Guid? SessionId { get; set; }

    public string? UserAgent { get; set; } = "unit-test";
}

public sealed class FakeTenantSettings : ITenantSettings
{
    public string? GoogleClientId { get; set; } = "client-id";
}
