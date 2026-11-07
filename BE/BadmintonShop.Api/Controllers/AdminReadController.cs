using BadmintonShop.Api.Catalog;
using BadmintonShop.Api.Engagement;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
public sealed class AdminReadController(CatalogService catalog, ReviewService reviews) : ControllerBase
{
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] ProductPageQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.ListAdminProductsAsync(query, ct)));
    [HttpGet("reviews")]
    public async Task<IActionResult> Reviews(CancellationToken ct, int page = 1, int pageSize = 12) =>
        Ok(ApiResponse.Ok(await reviews.AdminListAsync(page, pageSize, ct)));
}

