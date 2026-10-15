using Google.Apis.Auth;
using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Security;

/// <summary>Checks a Google Identity Services ID token's signature, issuer, expiry and audience (the tenant's client id).</summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string credential, string clientId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // The Google library takes no CancellationToken.
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.GivenName, payload.FamilyName);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
