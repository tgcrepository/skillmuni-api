using System.Collections.Concurrent;
using Cv.Application;
using Cv.Application.Parsing;
using Cv.Application.Services;
using Cv.Domain;
using Cv.Infrastructure.Reading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Cv.UnitTests;

public sealed class CvStore : ICvRepository, IUnitOfWork, IOutbox
{
    public List<CvDocument> Documents { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public int Saves { get; private set; }

    public Task<CvDocument?> FindAsync(Guid cvId, Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.Id == cvId && d.LearnerId == learnerId));

    public Task<CvDocument?> GetAsync(Guid cvId, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.Id == cvId));

    public Task<int> CountUploadsSinceAsync(Guid learnerId, DateTime since, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.Count(d => d.LearnerId == learnerId && d.CreatedAt >= since));

    public Task<IReadOnlyList<CvDocument>> ListForLearnerAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CvDocument>>(Documents.Where(d => d.LearnerId == learnerId).ToList());

    public void Add(CvDocument document) => Documents.Add(document);

    public void Remove(CvDocument document) => Documents.Remove(document);

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

public sealed class MemoryBlobs : IBlobStore
{
    public ConcurrentDictionary<string, (byte[] Bytes, string Type)> Blobs { get; } = new();

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Blobs[name] = (buffer.ToArray(), contentType);
    }

    public Task<StoredBlob?> OpenReadAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.TryGetValue(name, out var b) ? new StoredBlob(new MemoryStream(b.Bytes), b.Type) : null);

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        Blobs.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}

public sealed class NoopSignal : ICvParseSignal
{
    public int Count { get; private set; }

    public void Notify() => Count++;
}

/// <summary>The CV use cases wired to fakes, the sandbox reader and a controllable clock.</summary>
public sealed class CvHarness
{
    public CvHarness()
    {
        var options = MsOptions.Create(Settings);
        Service = new CvService(Store, Blobs, Signal, Store, Store, Tenant, Clock, options, NullLogger<CvService>.Instance);
        Job = new CvParseJob(Store, Blobs, Reader, new CvInterpreter(), Accounts, Store, Store, Tenant, Clock, options, NullLogger<CvParseJob>.Instance);
    }

    public CvServiceOptions Settings { get; } = new();

    public CvStore Store { get; } = new();

    public MemoryBlobs Blobs { get; } = new();

    public NoopSignal Signal { get; } = new();

    public ICvTextReader Reader { get; set; } = new SandboxCvTextReader();

    public IAccountDirectory Accounts { get; } = new SandboxAccountDirectory();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));

    public ITenantContext Tenant { get; } = new FixedTenant();

    public CvService Service { get; }

    public CvParseJob Job { get; }

    public static byte[] Pdf(string body = "") => System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n" + body + "\n%%EOF");

    public static UploadedFile File(byte[] bytes, string name = "asha-cv.pdf") => new(name, "application/pdf", bytes.Length, new MemoryStream(bytes));

    /// <summary>Uploads, runs the parse job and returns the CV id.</summary>
    public async Task<(Guid LearnerId, Guid CvId)> UploadAndParseAsync(string fileName, byte[]? bytes = null, string market = "IN")
    {
        var learner = Guid.NewGuid();
        var uploaded = await Service.UploadAsync(learner, market, [File(bytes ?? Pdf(), fileName)], CancellationToken.None);
        var id = Guid.Parse(uploaded.Value.CvId);
        Store.Documents.Single(d => d.Id == id).Start(Clock.GetUtcNow().UtcDateTime);
        await Job.RunAsync(id, CancellationToken.None);
        return (learner, id);
    }

    private sealed class FixedTenant : ITenantContext
    {
        public string TenantId => "skillmuni-in";

        public string Market => "IN";
    }
}
