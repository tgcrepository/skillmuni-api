using Learner.Application.Abstractions;
using Learner.Application.Contracts;

namespace Learner.Infrastructure.External;

/// <summary>
/// <c>Learner:DigiLocker</c>. <c>Mode</c> is <c>off</c> (default) or <c>sandbox</c> (Development and Testing only).
/// The live partner API needs DigiLocker issuer/requester credentials (MeitY onboarding), still open.
/// </summary>
public sealed class DigiLockerOptions
{
    public const string Section = "Learner:DigiLocker";

    public const string Sandbox = "sandbox";

    public string Mode { get; set; } = "off";
}

/// <summary>Until the partner credentials exist: every DigiLocker call answers 503 digilocker-unavailable.</summary>
internal sealed class UnavailableDigiLockerClient : IDigiLockerClient
{
    public bool IsAvailable => false;

    public Uri AuthorizeUrl(string document, string state) => throw new InvalidOperationException("DigiLocker is not configured.");

    public Task<Lookup<DigiLockerDocument>> FetchDocumentAsync(string document, string code, CancellationToken cancellationToken) =>
        Task.FromResult(Lookup<DigiLockerDocument>.Unavailable());

    public Task<Lookup<IReadOnlyList<ImportedTrophyDto>>> ImportCertificatesAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Lookup<IReadOnlyList<ImportedTrophyDto>>.Unavailable());
}

/// <summary>
/// Development sandbox matching the frontend mock (identity.handlers.ts, flex.handlers.ts): the consent "page" is the
/// quest itself, any code but <c>invalid</c> links, and the import returns two sample certificates.
/// </summary>
internal sealed class SandboxDigiLockerClient : IDigiLockerClient
{
    private static readonly Dictionary<string, (string Masked, string Last4)> Masks = new(StringComparer.Ordinal)
    {
        ["aadhaar"] = ("XXXX XXXX 4821", "4821"),
        ["pan"] = ("XXXXXX234F", "234F"),
        ["driving-licence"] = ("XXXX XXXXXXX7731", "7731"),
    };

    private static readonly IReadOnlyList<ImportedTrophyDto> Certificates =
    [
        new("NPTEL Programming in Java", "NPTEL · IIT Kharagpur", "2023-10", "NPTEL23CS74S4"),
        new("Skill India: Customer Care Executive", "NSDC · Skill India", "2024-03", "SI-CCE-88213"),
    ];

    public bool IsAvailable => true;

    public Uri AuthorizeUrl(string document, string state) =>
        new($"/quests/who-dis?part=2&digilocker=sandbox&state={Uri.EscapeDataString(state)}", UriKind.Relative);

    public Task<Lookup<DigiLockerDocument>> FetchDocumentAsync(string document, string code, CancellationToken cancellationToken) =>
        Task.FromResult(code == "invalid" || !Masks.TryGetValue(document, out var mask)
            ? Lookup<DigiLockerDocument>.NotFound()
            : Lookup<DigiLockerDocument>.Found(new DigiLockerDocument(mask.Masked, mask.Last4, new DateOnly(2004, 8, 21), Name: null)));

    public Task<Lookup<IReadOnlyList<ImportedTrophyDto>>> ImportCertificatesAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Lookup<IReadOnlyList<ImportedTrophyDto>>.Found(Certificates));
}
