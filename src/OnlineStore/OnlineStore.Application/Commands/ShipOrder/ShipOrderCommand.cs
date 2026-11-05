using MediatR;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Application.Commands.ShipOrder;

public sealed record ShipOrderCommand(Guid CustomerId, Guid OrderId) : IRequest;

public sealed class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand>
{
    private readonly IOrderWriteStore _store;
    public ShipOrderCommandHandler(IOrderWriteStore store) => _store = store;

    public Task Handle(ShipOrderCommand request, CancellationToken ct)
        => _store.MarkShippedAsync(request.CustomerId, request.OrderId, DateTimeOffset.UtcNow, ct);
}