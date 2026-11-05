using Moq;
using OnlineStore.Application.Abstractions;
using OnlineStore.Application.Commands.ShipOrder;

namespace OnlineStore.Tests;

public sealed class ShipOrderCommandHandlerTests
{
    private readonly Mock<IOrderWriteStore> _store = new();

    [Fact]
    public async Task Handle_marks_the_order_shipped_with_both_identifiers()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sut = new ShipOrderCommandHandler(_store.Object);

        await sut.Handle(new ShipOrderCommand(customerId, orderId), CancellationToken.None);

        _store.Verify(s => s.MarkShippedAsync(
            customerId, orderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}