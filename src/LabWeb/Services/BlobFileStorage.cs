using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace LabWeb.Services;

public class BlobStorageOptions
{
    public string? ConnectionString { get; set; }
    public string Container { get; set; } = "uploads";
    public string? PublicBaseUrl { get; set; }
}

public class BlobFileStorage(BlobContainerClient container, BlobStorageOptions options, UploadRules rules) : IFileStorage
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _containerReady;

    private string BaseUrl => (options.PublicBaseUrl ?? container.Uri.ToString()).TrimEnd('/');

    public string? Validate(IFormFile file) => rules.Validate(file);

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        var name = $"{folder}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = file.OpenReadStream();
        await container.GetBlobClient(name).UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType, CacheControl = "public, max-age=31536000, immutable" }
        }, ct);
        return $"{BaseUrl}/{name}";
    }

    public async Task DeleteAsync(string? path, CancellationToken ct = default)
    {
        var prefix = BaseUrl + "/";
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(prefix, StringComparison.Ordinal)) return;
        await container.GetBlobClient(path[prefix.Length..]).DeleteIfExistsAsync(cancellationToken: ct);
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerReady) return;
        await Gate.WaitAsync(ct);
        try
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
            _containerReady = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
