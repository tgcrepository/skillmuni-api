using System.Collections.Concurrent;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using SkillMuni.Common.Storage;

namespace Learner.UnitTests.Fakes;

public sealed class FakeBlobStore : IBlobStore
{
    public ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = new();

    public List<string> Deleted { get; } = [];

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Blobs[name] = (buffer.ToArray(), contentType);
    }

    public Task<StoredBlob?> OpenReadAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.TryGetValue(name, out var b) ? new StoredBlob(new MemoryStream(b.Bytes), b.ContentType) : null);

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        Deleted.Add(name);
        Blobs.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}

/// <summary>Reversible and obviously not plaintext, so tests can check the account number never lands as-is.</summary>
public sealed class FakeEncryptor : IFieldEncryptor
{
    public string Encrypt(string plaintext) => "enc:" + new string(plaintext.Reverse().ToArray());

    public string Decrypt(string ciphertext) => new(ciphertext[4..].Reverse().ToArray());
}

public sealed class FakeBankDirectory : IBankDirectory
{
    public Lookup<BankBranchInfo> Next { get; set; } = Lookup<BankBranchInfo>.Found(new BankBranchInfo("HDFC Bank", "Andheri West, Mumbai"));

    public Task<Lookup<BankBranchInfo>> FindIfscAsync(string ifsc, CancellationToken cancellationToken) => Task.FromResult(Next);
}

public sealed class FakeEsco : IEscoClient
{
    public Lookup<EscoSearchDto> Next { get; set; } = Lookup<EscoSearchDto>.Found(new EscoSearchDto(1, [new EscoSkillDto("http://data.europa.eu/esco/skill/x", "communicate with customers", "skill", "people", null, null)]));

    public (string Text, string Language, int Limit)? LastCall { get; private set; }

    public Task<Lookup<EscoSearchDto>> SearchAsync(string text, string language, int limit, CancellationToken cancellationToken)
    {
        LastCall = (text, language, limit);
        return Task.FromResult(Next);
    }
}

public sealed class FakeLinkChecker : ILinkChecker
{
    public Lookup<bool> Next { get; set; } = Lookup<bool>.Found(true);

    public IReadOnlyList<string>? Names { get; private set; }

    public Task<Lookup<bool>> PageMentionsAsync(Uri url, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        Names = names;
        return Task.FromResult(Next);
    }
}

public sealed class FakeDigiLocker : IDigiLockerClient
{
    public bool IsAvailable { get; set; } = true;

    public Lookup<DigiLockerDocument> Next { get; set; } =
        Lookup<DigiLockerDocument>.Found(new DigiLockerDocument("XXXX XXXX 4821", "4821", new DateOnly(2004, 8, 21), "Asha Rao"));

    public Uri AuthorizeUrl(string document, string state) => new($"https://digilocker.example/authorize?doc={document}&state={state}");

    public Task<Lookup<DigiLockerDocument>> FetchDocumentAsync(string document, string code, CancellationToken cancellationToken) =>
        Task.FromResult(Next);

    public Task<Lookup<IReadOnlyList<ImportedTrophyDto>>> ImportCertificatesAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Lookup<IReadOnlyList<ImportedTrophyDto>>.Found([new ImportedTrophyDto("NPTEL Java", "NPTEL", "2023-10", "N1")]));
}
