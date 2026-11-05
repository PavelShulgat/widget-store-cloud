using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Infrastructure.Blobs;

public sealed class AzureImageStore : IImageStore
{
    public const string ContainerName = "product-images";
    private readonly BlobContainerClient _container;

    public AzureImageStore(string connectionString)
    {
        _container = new BlobContainerClient(connectionString, ContainerName);
        // PublicAccessType.Blob: images are public by URL but the container is not listable.
        _container.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadAsync(Guid productId, string fileName, Stream content,
        string contentType, CancellationToken ct = default)
    {
        // Virtual folder per product: blobs are a flat namespace, but the "/" prefix
        // makes "all images for product X" a cheap prefix listing.
        var extension = Path.GetExtension(fileName);
        var blobName = $"{productId}/{Guid.NewGuid():N}{extension}";

        var blob = _container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        return blob.Uri.ToString();
    }

    public async Task<bool> DeleteAsync(string blobName, CancellationToken ct = default)
        => await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
}