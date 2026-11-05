namespace OnlineStore.Application.Abstractions;

public interface IImageStore
{
    Task<string> UploadAsync(Guid productId, string fileName, Stream content,
        string contentType, CancellationToken ct = default);

    Task<bool> DeleteAsync(string blobName, CancellationToken ct = default);
}