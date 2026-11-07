using MediatR;
using OnlineStore.Application.Abstractions;
using OnlineStore.Domain.Entities;

namespace OnlineStore.Application.Queries.GetProducts;

public sealed record GetProductsQuery : IRequest<IReadOnlyList<Product>>;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<Product>>
{
    private readonly IProductRepository _products;
    public GetProductsQueryHandler(IProductRepository products) => _products = products;

    public Task<IReadOnlyList<Product>> Handle(GetProductsQuery request, CancellationToken ct)
        => _products.ListAsync(ct);
}