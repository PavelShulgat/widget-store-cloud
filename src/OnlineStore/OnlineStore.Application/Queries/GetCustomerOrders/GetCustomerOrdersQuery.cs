using MediatR;
using OnlineStore.Application.Abstractions;
using OnlineStore.Domain.Entities;

namespace OnlineStore.Application.Queries.GetCustomerOrders;

public sealed record GetCustomerOrdersQuery(Guid CustomerId, int Take = 50)
    : IRequest<IReadOnlyList<Order>>;

public sealed class GetCustomerOrdersQueryHandler
    : IRequestHandler<GetCustomerOrdersQuery, IReadOnlyList<Order>>
{
    private readonly IOrderReadStore _store;
    public GetCustomerOrdersQueryHandler(IOrderReadStore store) => _store = store;

    public Task<IReadOnlyList<Order>> Handle(GetCustomerOrdersQuery request, CancellationToken ct)
        => _store.GetForCustomerAsync(request.CustomerId, request.Take, ct);
}