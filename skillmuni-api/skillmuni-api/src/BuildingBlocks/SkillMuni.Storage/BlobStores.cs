using System.Collections.Concurrent;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Storage;

namespace SkillMuni.Storage;

/// <summary>
/// Blob settings (each service's <c>&lt;Service&gt;:Storage</c> section). In Azure set <see cref="ServiceUri"/>: the app
/// signs in with its managed identity (Storage Blob Data Contributor on its own container only). Locally,
/// <see cref="ConnectionString"/> points at Azurite; with neither, files are kept in memory (local runs only).
/// </summary>
public sealed class BlobStorageOptions
{
    public string? ServiceUri { get; set; }

    public string? ConnectionString { get; set; }

    public string Container { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUri) || !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>Private container; files are only ever streamed back through the API, never by public URL.</summary>
public sealed class AzureBlobStore : IBlobStore
{
    private readonly BlobContainerClient _container;
    private readonly bool _createContainer;
    private int _created;

    public AzureBlobStore(IOptions<BlobStorageOptions> options)
    {
        var settings = options.Value;
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            _container = new BlobContainerClient(settings.ConnectionString, settings.Container);

            // Azurite starts empty; in Azure the container comes from Bicep and the app can't create containers.
            _createContainer = true;
        }
        else
        {
            var service = new BlobServiceClient(new Uri(settings.ServiceUri!), new DefaultAzureCredential());
            _container = service.GetBlobContainerClient(settings.Container);
        }
    }

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (_createContainer && Interlocked.Exchange(ref _created, 1) == 0)
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        }

        await _container.GetBlobClient(name).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    public async Task<StoredBlob?> OpenReadAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _container.GetBlobClient(name).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new StoredBlob(download.Value.Content, download.Value.Details.ContentType);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken) =>
        _container.GetBlobClient(name).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
}

/// <summary>Development and tests without Azurite: files live in memory until the process stops.</summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _blobs = new(StringComparer.Ordinal);

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _blobs[name] = (buffer.ToArray(), contentType);
    }

    public Task<StoredBlob?> OpenReadAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(_blobs.TryGetValue(name, out var blob)
            ? new StoredBlob(new MemoryStream(blob.Bytes, writable: false), blob.ContentType)
            : null);

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        _blobs.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
