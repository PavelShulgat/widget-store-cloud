using MediatR;
using Microsoft.AspNetCore.Mvc;
using OnlineStore.Application.Commands.SetProductImage;
using OnlineStore.Application.Commands.UploadProductImage;

namespace OnlineStore.Api.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/images")]
public sealed class ProductImagesController : ControllerBase
{
    private readonly ISender _sender;
    public ProductImagesController(ISender sender) => _sender = sender;

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid productId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file supplied.");

        await using var stream = file.OpenReadStream();

        var url = await _sender.Send(
            new UploadProductImageCommand(productId, file.FileName, stream, file.ContentType), ct);

        await _sender.Send(new SetProductImageCommand(productId, url), ct);

        return Created(url, new { url });
    }
}