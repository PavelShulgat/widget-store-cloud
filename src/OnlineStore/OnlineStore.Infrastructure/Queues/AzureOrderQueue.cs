using System.Text.Json;
using Azure.Storage.Queues;
using OnlineStore.Application.Abstractions;

namespace OnlineStore.Infrastructure.Queues;

public sealed class AzureOrderQueue : IOrderQueue
{
    public const string QueueName = "orders";
    private readonly QueueClient _client;

    public AzureOrderQueue(string connectionString)
    {
        _client = new QueueClient(connectionString, QueueName,
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        _client.CreateIfNotExists();
    }

    public async Task EnqueueAsync(OrderPlacedMessage message, CancellationToken ct = default)
        => await _client.SendMessageAsync(JsonSerializer.Serialize(message), ct);
}