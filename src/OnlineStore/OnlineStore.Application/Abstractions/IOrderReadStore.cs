using OnlineStore.Domain.Entities;

namespace OnlineStore.Application.Abstractions;

public interface IOrderReadStore
{
    Task<Order?> GetAsync(Guid customerId, Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetForCustomerAsync(Guid customerId, int take = 50, CancellationToken ct = default);
}