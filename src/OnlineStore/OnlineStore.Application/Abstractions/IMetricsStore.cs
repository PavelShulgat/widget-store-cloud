namespace OnlineStore.Application.Abstractions;

public interface IMetricsStore
{
    Task SaveAsync(string metricName, double value, DateTimeOffset computedAt, CancellationToken ct = default);
}