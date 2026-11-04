using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Functions;

public sealed class ProcessOrderFunction
{
    private readonly ILogger<ProcessOrderFunction> _logger;
    public ProcessOrderFunction(ILogger<ProcessOrderFunction> logger) => _logger = logger;

    [Function(nameof(ProcessOrderFunction))]
    public Task Run([QueueTrigger("orders", Connection = "AzureWebJobsStorage")] string body)
    {
        var message = JsonSerializer.Deserialize<OrderPlacedMessage>(body)!;
        _logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
            message.OrderId, message.CustomerId);
        // next step: persist to Table Storage with PartitionKey = CustomerId
        return Task.CompletedTask;
    }
}