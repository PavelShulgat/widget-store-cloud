using Azure;
using Azure.Data.Tables;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Infrastructure.Tables;

public sealed class MetricEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;   // metric name
    public string RowKey { get; set; } = default!;         // inverted ticks: newest first
    public double Value { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public sealed class AzureMetricsStore : IMetricsStore
{
    public const string TableName = "metrics";
    private readonly TableClient _table;

    public AzureMetricsStore(string connectionString)
    {
        _table = new TableClient(connectionString, TableName);
        _table.CreateIfNotExists();
    }

    public async Task SaveAsync(string metricName, double value, DateTimeOffset computedAt, CancellationToken ct = default)
        => await _table.UpsertEntityAsync(new MetricEntity
        {
            PartitionKey = metricName,
            RowKey = $"{DateTimeOffset.MaxValue.Ticks - computedAt.UtcTicks:D19}",
            Value = value,
            ComputedAt = computedAt
        }, TableUpdateMode.Replace, ct);
}