namespace OnlineStore.Application.Abstractions;

public interface IOrderQueue
{
    Task EnqueueAsync(OrderPlacedMessage message, CancellationToken ct = default);
}

public sealed record OrderPlacedMessage(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderLineMessage> Lines,
    DateTimeOffset OrderDate);

public sealed record OrderLineMessage(Guid ProductId, int Quantity, decimal UnitPrice);