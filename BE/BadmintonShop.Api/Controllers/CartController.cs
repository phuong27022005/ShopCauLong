using System.Security.Claims;
using BadmintonShop.Api.Cart;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public sealed class CartController(CartService cart) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue("sub"), out var id) && id > 0
        ? id : throw new CatalogException(401, "Token không có userId hợp lệ.");

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await cart.GetAsync(UserId, ct)));

    [HttpPost("items")]
    public async Task<IActionResult> Add(AddCartItemRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await cart.AddAsync(UserId, input, ct)));

    [HttpPut("items/{productId:int}")]
    public async Task<IActionResult> Update(int productId, UpdateCartItemRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await cart.UpdateAsync(UserId, productId, input.Quantity, ct)));

    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> Delete(int productId, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await cart.DeleteAsync(UserId, productId, ct)));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await cart.DeleteAsync(UserId, null, ct)));

    [HttpPost("merge")]
    public async Task<IActionResult> Merge(MergeCartRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await cart.MergeAsync(UserId, input, ct)));
}

