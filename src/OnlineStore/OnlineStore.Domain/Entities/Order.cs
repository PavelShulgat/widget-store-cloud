namespace OnlineStore.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid CustomerId { get; init; }
    public required IReadOnlyList<OrderLine> Lines { get; init; }
    public DateTimeOffset OrderDate { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ShipDate { get; set; }
}

public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);