using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/brands")]
public sealed class BrandsController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.ListBrandsAsync(ct)));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(SaveBrandRequest input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(await catalog.SaveBrandAsync(null, input, ct)));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(int id, SaveBrandRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.SaveBrandAsync(id, input, ct)));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteBrandAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
