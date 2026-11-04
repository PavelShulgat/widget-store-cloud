using OnlineStore.Domain.Entities;

namespace OnlineStore.Application.Abstractions;

public interface IOrderWriteStore
{
    Task SaveAsync(Order order, CancellationToken ct = default);
    Task MarkShippedAsync(Guid customerId, Guid orderId, DateTimeOffset shipDate, CancellationToken ct = default);
}