namespace Identity.Application.Contracts;

// Request bodies, exactly as the Angular app sends them (src/app/core/**/*.api.ts).

public sealed record SendPhoneCodeRequest(string Phone);

public sealed record VerifyPhoneCodeRequest(string Phone, string Code);

public sealed record SendEmailCodeRequest(string Email);

public sealed record VerifyEmailCodeRequest(string Email, string Code);

public sealed record PasswordLoginRequest(string Email, string Password);

public sealed record PasswordResetRequest(string Email);

/// <summary>New (not in the frontend yet): completes a reset from the emailed link.</summary>
public sealed record PasswordResetConfirmRequest(string Token, string Password);

public sealed record RegisterRequest(string FirstName, string LastName, string Email, string Password, bool Consent);

public sealed record GoogleSignInRequest(string Credential);

/// <summary>Gateway → Identity, internal only.</summary>
public sealed record RefreshSessionRequest(string RefreshToken);

/// <summary><c>kind</c> is <c>mobile</c> or <c>email</c> (A-273).</summary>
public sealed record ContactCodeRequest(string Kind, string Value);

public sealed record ContactVerifyRequest(string Kind, string Value, string Code);

public sealed record ClaimCodeRequest(string Code);

public sealed record ClaimConsentRequest(bool ShareProgress, bool VerifyCertificate, bool ApplyForMe);

/// <summary><c>reason</c>: not-me · not-now · has-account · other · null.</summary>
public sealed record ClaimDeclineRequest(string? Reason);

/// <summary>MISS-07: a consent given or changed in the app.</summary>
public sealed record RecordConsentRequest(string Scope, string PolicyVersion, bool Granted);

/// <summary>Institute admin: create the claim link for an uploaded CV.</summary>
public sealed record CreateClaimRequest(
    string Institute,
    string Name,
    string Programme,
    string Batch,
    string FromCv,
    string Phone,
    string Certificate,
    DateOnly DeleteBy,
    IReadOnlyList<ClaimSectionInput> Sections,
    ClaimResultDto? Result);

public sealed record ClaimSectionInput(string Slug, int Quest, string Title, string Summary, IReadOnlyList<ClaimRowDto> Rows);
