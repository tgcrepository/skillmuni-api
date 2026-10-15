namespace SkillMuni.Common.Storage;

public sealed record StoredBlob(Stream Content, string ContentType);

/// <summary>
/// A service's private file container (Azure Blob Storage through its managed identity). Names are opaque paths;
/// nothing personal goes in a blob name. Files are only ever streamed back through the API.
/// </summary>
public interface IBlobStore
{
    Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken);

    Task<StoredBlob?> OpenReadAsync(string name, CancellationToken cancellationToken);

    /// <summary>Succeeds when the blob is already gone, so erasure can be retried.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken);
}
