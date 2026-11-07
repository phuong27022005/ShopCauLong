using System.Security.Claims;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Api.Orders;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue("sub"), out var id) && id > 0
        ? id : throw new CatalogException(401, "Token không có userId hợp lệ.");
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest input, CancellationToken ct)
    {
        var order = await orders.CreateAsync(UserId, input, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, ApiResponse.Ok(order));
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await orders.ListAsync(UserId, query, ct)));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await orders.GetAsync(id, UserId, ct)));
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await orders.TransitionAsync(id, UserId, "Cancelled", ct)));
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(OrderService orders) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await orders.ListAsync(null, query, ct)));
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> Status(int id, UpdateOrderStatusRequest input, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await orders.TransitionAsync(id, null, input.Status, ct)));
}
