using MediatR;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Application.Commands.UploadProductImage;

public sealed record UploadProductImageCommand(
    Guid ProductId,
    string FileName,
    Stream Content,
    string ContentType) : IRequest<string>;

public sealed class UploadProductImageCommandHandler
    : IRequestHandler<UploadProductImageCommand, string>
{
    private static readonly HashSet<string> Allowed =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IImageStore _images;
    public UploadProductImageCommandHandler(IImageStore images) => _images = images;

    public Task<string> Handle(UploadProductImageCommand request, CancellationToken ct)
    {
        var extension = Path.GetExtension(request.FileName);

        if (!Allowed.Contains(extension))
            throw new ArgumentException($"Unsupported image type '{extension}'.");

        return _images.UploadAsync(request.ProductId, request.FileName,
            request.Content, request.ContentType, ct);
    }
}