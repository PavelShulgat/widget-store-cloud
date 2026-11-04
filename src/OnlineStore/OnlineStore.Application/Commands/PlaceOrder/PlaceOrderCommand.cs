using MediatR;

namespace OnlineStore.Application.Commands.PlaceOrder;

public sealed record PlaceOrderCommand(
    Guid CustomerId,
    IReadOnlyList<PlaceOrderLine> Lines) : IRequest<Guid>;

public sealed record PlaceOrderLine(Guid ProductId, int Quantity, decimal UnitPrice);