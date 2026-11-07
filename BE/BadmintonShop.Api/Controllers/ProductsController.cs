using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductPageQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.ListProductsAsync(query, ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.GetProductAsync(slug, ct)));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(SaveProductRequest input, CancellationToken ct)
    {
        var product = await catalog.SaveProductAsync(null, input, ct);
        return product.IsActive
            ? CreatedAtAction(nameof(Detail), new { slug = product.Slug }, ApiResponse.Ok(product))
            : StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(product));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(int id, SaveProductRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.SaveProductAsync(id, input, ct)));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteProductAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("{id:int}/image")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(int id, IFormFile? file, CancellationToken ct)
    {
        var imageUrl = await catalog.UploadImageAsync(id, file, ct);
        return Ok(ApiResponse.Ok(new { imageUrl }));
    }
}
