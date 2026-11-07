using MediatR;
using OnlineStore.Application.Abstractions;
using OnlineStore.Domain.Entities;

namespace OnlineStore.Application.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name, string Description, decimal Price, int StockQuantity) : IRequest<Guid>;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _products;
    public CreateProductCommandHandler(IProductRepository products) => _products = products;

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        await _products.AddAsync(product, ct);
        return product.Id;
    }
}