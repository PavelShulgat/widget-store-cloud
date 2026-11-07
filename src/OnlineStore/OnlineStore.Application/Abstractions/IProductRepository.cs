using OnlineStore.Domain.Entities;

namespace OnlineStore.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task SetImageUrlAsync(Guid id, string imageUrl, CancellationToken ct = default);
}