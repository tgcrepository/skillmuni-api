using Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's PBKDF2 hasher (V3 format), without the rest of ASP.NET Core Identity.</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly PasswordOwner Owner = new();
    private readonly PasswordHasher<PasswordOwner> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Owner, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(Owner, passwordHash, password) != PasswordVerificationResult.Failed;

    /// <summary>The framework hasher wants a user object it never reads.</summary>
    private sealed class PasswordOwner
    {
    }
}
