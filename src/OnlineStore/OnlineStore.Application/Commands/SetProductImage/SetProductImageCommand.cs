using MediatR;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Application.Commands.SetProductImage;

public sealed record SetProductImageCommand(Guid ProductId, string ImageUrl) : IRequest;

public sealed class SetProductImageCommandHandler : IRequestHandler<SetProductImageCommand>
{
    private readonly IProductRepository _products;
    public SetProductImageCommandHandler(IProductRepository products) => _products = products;

    public Task Handle(SetProductImageCommand request, CancellationToken ct)
        => _products.SetImageUrlAsync(request.ProductId, request.ImageUrl, ct);
}