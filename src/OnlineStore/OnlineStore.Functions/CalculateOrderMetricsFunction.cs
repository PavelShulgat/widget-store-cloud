using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Functions;

public sealed class CalculateOrderMetricsFunction
{
    private readonly IOrderReadStore _orders;
    private readonly IMetricsStore _metrics;
    private readonly ILogger<CalculateOrderMetricsFunction> _logger;

    public CalculateOrderMetricsFunction(
        IOrderReadStore orders,
        IMetricsStore metrics,
        ILogger<CalculateOrderMetricsFunction> logger)
    {
        _orders = orders;
        _metrics = metrics;
        _logger = logger;
    }

    // Every 5 minutes while developing; nightly ("0 0 2 * * *") in production.
    [Function(nameof(CalculateOrderMetricsFunction))]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-1);
        var shipped = await _orders.GetShippedSinceAsync(since);

        var withShipDate = shipped.Where(o => o.ShipDate.HasValue).ToList();

        if (withShipDate.Count == 0)
        {
            _logger.LogInformation("No shipped orders since {Since}; nothing to compute.", since);
            return;
        }

        var averageHours = withShipDate.Average(o => (o.ShipDate!.Value - o.OrderDate).TotalHours);

        await _metrics.SaveAsync("orderprocessed", averageHours, DateTimeOffset.UtcNow);

        _logger.LogInformation("orderprocessed = {Hours:F2}h over {Count} orders",
            averageHours, withShipDate.Count);
    }
}