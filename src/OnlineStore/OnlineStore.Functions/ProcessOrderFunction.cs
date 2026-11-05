using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OnlineStore.Application.Abstractions;
using OnlineStore.Domain.Entities;

namespace OnlineStore.Functions;

public sealed class ProcessOrderFunction
{
    private readonly IOrderWriteStore _store;
    private readonly ILogger<ProcessOrderFunction> _logger;

    public ProcessOrderFunction(IOrderWriteStore store, ILogger<ProcessOrderFunction> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Function(nameof(ProcessOrderFunction))]
    public async Task Run([QueueTrigger("orders", Connection = "AzureWebJobsStorage")] string body)
    {
        var message = JsonSerializer.Deserialize<OrderPlacedMessage>(body)!;

        var order = new Order
        {
            Id = message.OrderId,
            CustomerId = message.CustomerId,
            Lines = message.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice)).ToList(),
            OrderDate = message.OrderDate
        };

        await _store.SaveAsync(order);
        _logger.LogInformation("Persisted order {OrderId}", order.Id);
    }
}