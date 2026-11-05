using MediatR;
using Microsoft.AspNetCore.Mvc;
using OnlineStore.Application.Commands.PlaceOrder;
using OnlineStore.Application.Commands.ShipOrder;
using OnlineStore.Application.Queries.GetCustomerOrders;

namespace OnlineStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;
    public OrdersController(ISender sender) => _sender = sender;

    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderCommand command, CancellationToken ct)
    {
        var orderId = await _sender.Send(command, ct);
        return Accepted(new { orderId });
    }

    [HttpGet]
    public async Task<IActionResult> GetForCustomer([FromQuery] Guid customerId, CancellationToken ct)
        => Ok(await _sender.Send(new GetCustomerOrdersQuery(customerId), ct));

    [HttpPatch("{orderId:guid}/ship")]
    public async Task<IActionResult> Ship(Guid orderId, [FromQuery] Guid customerId, CancellationToken ct)
    {
        await _sender.Send(new ShipOrderCommand(customerId, orderId), ct);
        return NoContent();
    }
}