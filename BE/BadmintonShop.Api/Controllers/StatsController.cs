using BadmintonShop.Api.Catalog;
using BadmintonShop.Api.Stats;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/stats")]
public sealed class StatsController(StatsService stats) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] StatsQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await stats.OverviewAsync(query, ct)));

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] StatsQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await stats.RevenueAsync(query, ct)));

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] TopProductsQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await stats.TopProductsAsync(query.Limit, ct)));
}

