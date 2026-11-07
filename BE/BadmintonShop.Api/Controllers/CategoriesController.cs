using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.ListCategoriesAsync(ct)));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(SaveCategoryRequest input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(await catalog.SaveCategoryAsync(null, input, ct)));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(int id, SaveCategoryRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await catalog.SaveCategoryAsync(id, input, ct)));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteCategoryAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
