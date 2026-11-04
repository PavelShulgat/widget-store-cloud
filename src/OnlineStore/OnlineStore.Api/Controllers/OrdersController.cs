using MediatR;
using Microsoft.AspNetCore.Mvc;
using OnlineStore.Application.Commands.PlaceOrder;

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
}