using Moq;
using OnlineStore.Application.Abstractions;
using OnlineStore.Application.Commands.PlaceOrder;

namespace OnlineStore.Tests;

public sealed class PlaceOrderCommandHandlerTests
{
    private readonly Mock<IOrderQueue> _queue = new();

    private PlaceOrderCommandHandler CreateSut() => new(_queue.Object);

    private static PlaceOrderCommand ACommand(Guid? customerId = null) => new(
        customerId ?? Guid.NewGuid(),
        new List<PlaceOrderLine> { new(Guid.NewGuid(), 2, 9.99m) });

    [Fact]
    public async Task Handle_enqueues_exactly_one_message()
    {
        var sut = CreateSut();

        await sut.Handle(ACommand(), CancellationToken.None);

        _queue.Verify(q => q.EnqueueAsync(It.IsAny<OrderPlacedMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_returns_the_same_order_id_it_enqueued()
    {
        OrderPlacedMessage? captured = null;
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<OrderPlacedMessage>(), It.IsAny<CancellationToken>()))
              .Callback<OrderPlacedMessage, CancellationToken>((m, _) => captured = m)
              .Returns(Task.CompletedTask);

        var returnedId = await CreateSut().Handle(ACommand(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(returnedId, captured!.OrderId);
    }

    [Fact]
    public async Task Handle_maps_customer_and_lines_onto_the_message()
    {
        var customerId = Guid.NewGuid();
        OrderPlacedMessage? captured = null;
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<OrderPlacedMessage>(), It.IsAny<CancellationToken>()))
              .Callback<OrderPlacedMessage, CancellationToken>((m, _) => captured = m)
              .Returns(Task.CompletedTask);

        var command = ACommand(customerId);
        await CreateSut().Handle(command, CancellationToken.None);

        Assert.Equal(customerId, captured!.CustomerId);
        Assert.Equal(command.Lines.Count, captured.Lines.Count);
        Assert.Equal(command.Lines[0].ProductId, captured.Lines[0].ProductId);
        Assert.Equal(command.Lines[0].Quantity, captured.Lines[0].Quantity);
    }

    [Fact]
    public async Task Handle_propagates_queue_failures_rather_than_swallowing_them()
    {
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<OrderPlacedMessage>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().Handle(ACommand(), CancellationToken.None));
    }
}