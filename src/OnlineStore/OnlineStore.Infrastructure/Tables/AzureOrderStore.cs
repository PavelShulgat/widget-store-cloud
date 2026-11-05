using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using OnlineStore.Application.Abstractions;
using OnlineStore.Domain.Entities;

namespace OnlineStore.Infrastructure.Tables;

public sealed class AzureOrderStore : IOrderWriteStore, IOrderReadStore
{
    public const string TableName = "orders";
    private readonly TableClient _table;

    public AzureOrderStore(string connectionString)
    {
        _table = new TableClient(connectionString, TableName);
        _table.CreateIfNotExists();
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        var entity = new OrderEntity
        {
            PartitionKey = order.CustomerId.ToString(),
            RowKey = OrderEntity.BuildRowKey(order.OrderDate, order.Id),
            OrderId = order.Id.ToString(),
            LinesJson = JsonSerializer.Serialize(order.Lines),
            OrderDate = order.OrderDate,
            ShipDate = order.ShipDate
        };

        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task MarkShippedAsync(Guid customerId, Guid orderId, DateTimeOffset shipDate, CancellationToken ct = default)
    {
        var entity = await FindAsync(customerId, orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        entity.ShipDate = shipDate;
        await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
    }

    public async Task<Order?> GetAsync(Guid customerId, Guid orderId, CancellationToken ct = default)
    {
        var entity = await FindAsync(customerId, orderId, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<Order>> GetForCustomerAsync(Guid customerId, int take = 50, CancellationToken ct = default)
    {
        var results = new List<Order>();

        // Single-partition query: the cheapest access pattern Table Storage offers.
        var query = _table.QueryAsync<OrderEntity>(
            e => e.PartitionKey == customerId.ToString(), maxPerPage: take, cancellationToken: ct);

        await foreach (var entity in query)
        {
            results.Add(Map(entity));
            if (results.Count >= take) break;
        }

        return results; // already newest-first, thanks to the inverted-ticks RowKey
    }

    private async Task<OrderEntity?> FindAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var id = orderId.ToString();
        var query = _table.QueryAsync<OrderEntity>(
            e => e.PartitionKey == customerId.ToString() && e.OrderId == id, cancellationToken: ct);

        await foreach (var entity in query) return entity;
        return null;
    }

    public async Task<IReadOnlyList<Order>> GetShippedSinceAsync(DateTimeOffset since, CancellationToken ct = default)
{
    var filter = $"ShipDate ge datetime'{since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}'";

    var results = new List<Order>();
    var query = _table.QueryAsync<OrderEntity>(filter: filter, cancellationToken: ct);

    await foreach (var entity in query) results.Add(Map(entity));
    return results;
}

    private static Order Map(OrderEntity e) => new()
    {
        Id = Guid.Parse(e.OrderId),
        CustomerId = Guid.Parse(e.PartitionKey),
        Lines = JsonSerializer.Deserialize<List<OrderLine>>(e.LinesJson)!,
        OrderDate = e.OrderDate,
        ShipDate = e.ShipDate
    };
}