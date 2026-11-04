using MediatR;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Application.Commands.PlaceOrder;

public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly IOrderQueue _queue;

    public PlaceOrderCommandHandler(IOrderQueue queue) => _queue = queue;

    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();

        var message = new OrderPlacedMessage(
            orderId,
            request.CustomerId,
            request.Lines.Select(l => new OrderLineMessage(l.ProductId, l.Quantity, l.UnitPrice)).ToList(),
            DateTimeOffset.UtcNow);

        await _queue.EnqueueAsync(message, ct);
        return orderId;
    }
}