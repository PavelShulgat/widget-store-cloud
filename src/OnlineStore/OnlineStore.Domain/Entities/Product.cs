namespace OnlineStore.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }   // Blob URL — the bytes live in Blob Storage
}