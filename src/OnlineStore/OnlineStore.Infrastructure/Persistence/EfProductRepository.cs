using Microsoft.EntityFrameworkCore;
using OnlineStore.Application.Abstractions;
using OnlineStore.Domain.Entities;

namespace OnlineStore.Infrastructure.Persistence;

public sealed class EfProductRepository : IProductRepository
{
    private readonly ProductDbContext _db;
    public EfProductRepository(ProductDbContext db) => _db = db;

    public Task<Product?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default)
        => await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetImageUrlAsync(Guid id, string imageUrl, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"Product {id} not found.");

        product.ImageUrl = imageUrl;
        await _db.SaveChangesAsync(ct);
    }
}