using Azure;
using Azure.Data.Tables;

namespace OnlineStore.Infrastructure.Tables;

public sealed class OrderEntity : ITableEntity
{
    // PartitionKey = CustomerId: spreads writes across customers, no hot partition.
    // RowKey = inverted ticks: newest-first ordering without a sort, since Table
    // Storage only ever returns rows in ascending RowKey order.
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;

    public string OrderId { get; set; } = default!;
    public string LinesJson { get; set; } = default!;
    public DateTimeOffset OrderDate { get; set; }
    public DateTimeOffset? ShipDate { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public static string BuildRowKey(DateTimeOffset orderDate, Guid orderId)
        => $"{DateTimeOffset.MaxValue.Ticks - orderDate.UtcTicks:D19}_{orderId:N}";
}