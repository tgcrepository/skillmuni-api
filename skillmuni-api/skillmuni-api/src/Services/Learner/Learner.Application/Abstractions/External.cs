using Learner.Application.Contracts;

namespace Learner.Application.Abstractions;

public enum LookupStatus
{
    Found,
    NotFound,
    Timeout,
    Unavailable,
}

/// <summary>What an outside system said. Clients never throw for "not found", "slow" or "down".</summary>
public sealed record Lookup<T>(LookupStatus Status, T? Value)
{
    public static Lookup<T> Found(T value) => new(LookupStatus.Found, value);

    public static Lookup<T> NotFound() => new(LookupStatus.NotFound, default);

    public static Lookup<T> Timeout() => new(LookupStatus.Timeout, default);

    public static Lookup<T> Unavailable() => new(LookupStatus.Unavailable, default);
}

/// <summary>Field-level encryption for values that must be read back (bank account numbers). Key from Key Vault.</summary>
public interface IFieldEncryptor
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}

public sealed record BankBranchInfo(string Bank, string Branch);

/// <summary>India IFSC → bank and branch (Razorpay's public IFSC API, cached).</summary>
public interface IBankDirectory
{
    Task<Lookup<BankBranchInfo>> FindIfscAsync(string ifsc, CancellationToken cancellationToken);
}

/// <summary>A-245: ESCO's public skill search.</summary>
public interface IEscoClient
{
    Task<Lookup<EscoSearchDto>> SearchAsync(string text, string language, int limit, CancellationToken cancellationToken);
}

/// <summary>Q4 credential links: fetch a public page (5 s, no private networks) and look for the learner's name.</summary>
public interface ILinkChecker
{
    Task<Lookup<bool>> PageMentionsAsync(Uri url, IReadOnlyList<string> names, CancellationToken cancellationToken);
}

/// <summary>What DigiLocker returns after masking: never the full number or a copy of the document.</summary>
public sealed record DigiLockerDocument(string Masked, string Last4, DateOnly? Birthday, string? Name);

/// <summary>DigiLocker (India, A-272): consent URL, code exchange, certificate import.</summary>
public interface IDigiLockerClient
{
    /// <summary>False when no DigiLocker partner credentials are configured (outside Development).</summary>
    bool IsAvailable { get; }

    Uri AuthorizeUrl(string document, string state);

    Task<Lookup<DigiLockerDocument>> FetchDocumentAsync(string document, string code, CancellationToken cancellationToken);

    Task<Lookup<IReadOnlyList<ImportedTrophyDto>>> ImportCertificatesAsync(Guid learnerId, CancellationToken cancellationToken);
}
