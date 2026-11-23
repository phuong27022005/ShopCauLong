using System.Security.Claims;
using AutoMapper;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/discovery/categories")]
public sealed class CategoryDiscoveryController(ShopDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking()
            .Where(c => db.Products.Any(p => p.CategoryId == c.Id && p.IsActive && p.Stock > 0))
            .OrderBy(c => c.Id).Select(c => new { c.Id, c.Name, c.Slug,
                ImageUrl = db.Products.Where(p => p.CategoryId == c.Id && p.IsActive && p.Stock > 0 && p.ImageUrl != null)
                    .OrderBy(p => p.Id).Select(p => p.ImageUrl).FirstOrDefault(),
                ProductCount = db.Products.Count(p => p.CategoryId == c.Id && p.IsActive && p.Stock > 0) }).ToListAsync(ct);
        return Ok(ApiResponse.Ok(categories));
    }
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/inventory")]
public sealed class InventoryController(ShopDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ProductPageQuery query, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) products = products.Where(x => x.Name.Contains(query.Search.Trim()));
        var total = await products.CountAsync(ct);
        var skip = ((long)query.Page - 1) * query.PageSize;
        var selected = await products.OrderBy(x => x.Stock).ThenBy(x => x.Id).Skip((int)Math.Min(skip, int.MaxValue)).Take(query.PageSize)
            .Select(p => new { p.Id, p.Name, p.Stock, p.IsActive,
                Reserved = (from line in db.OrderItems join order in db.Orders on line.OrderId equals order.Id
                    where line.ProductId == p.Id && (order.Status == "Pending" || order.Status == "Confirmed")
                    select (int?)line.Quantity).Sum() ?? 0 }).ToListAsync(ct);
        return Ok(ApiResponse.Ok(new { Items = selected, query.Page, query.PageSize, TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / query.PageSize) }));
    }
}

[ApiController]
[Authorize]
[Route("api/favorites")]
public sealed class FavoritesController(ShopDbContext db, IMapper mapper) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var favorites = await db.Favorites.AsNoTracking().Include(x => x.Product).ThenInclude(x => x.Brand)
            .Where(x => x.UserId == UserId && x.Product.IsActive).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Ok(ApiResponse.Ok(mapper.Map<List<ProductListDto>>(favorites.Select(x => x.Product))));
    }
    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Add(int productId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == UserId, ct)) return Unauthorized(ApiResponse.Error("Tài khoản không tồn tại."));
        if (!await db.Products.AnyAsync(x => x.Id == productId && x.IsActive, ct)) throw CatalogException.NotFound("Sản phẩm");
        if (await db.Favorites.AnyAsync(x => x.UserId == UserId && x.ProductId == productId, ct)) return Ok(ApiResponse.Ok());
        if (await db.Favorites.CountAsync(x => x.UserId == UserId, ct) >= 200) throw CatalogException.BadRequest("Danh sách tối đa 200 sản phẩm.");
        db.Favorites.Add(new Favorite { UserId = UserId, ProductId = productId, CreatedAt = DateTime.UtcNow });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await db.Favorites.AnyAsync(x => x.UserId == UserId && x.ProductId == productId, ct)) throw;
        }
        return Ok(ApiResponse.Ok());
    }
    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Delete(int productId, CancellationToken ct)
    {
        await db.Favorites.Where(x => x.UserId == UserId && x.ProductId == productId).ExecuteDeleteAsync(ct);
        return Ok(ApiResponse.Ok());
    }
}

[ApiController]
[Route("api/products/compare")]
public sealed class CompareController(ShopDbContext db, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int[] ids, CancellationToken ct)
    {
        if (ids.Length is < 1 or > 3 || ids.Any(x => x < 1) || ids.Distinct().Count() != ids.Length)
            throw CatalogException.BadRequest("Chọn từ 1 đến 3 sản phẩm khác nhau để so sánh.");
        var products = await db.Products.AsNoTracking().Include(x => x.Brand).Include(x => x.Category)
            .Where(x => ids.Contains(x.Id) && x.IsActive).ToListAsync(ct);
        return Ok(ApiResponse.Ok(mapper.Map<List<ProductDetailDto>>(products.OrderBy(x => Array.IndexOf(ids, x.Id)))));
    }
}

[ApiController]
[Authorize]
public sealed class OrderHistoryController(ShopDbContext db) : ControllerBase
{
    [HttpGet("api/orders/{id:int}/history")]
    [HttpGet("api/admin/orders/{id:int}/history")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue("sub")!);
        if (!await db.Orders.AnyAsync(x => x.Id == id && (User.IsInRole("Admin") || x.UserId == userId), ct))
            throw CatalogException.NotFound("Đơn hàng");
        var events = await db.OrderHistories.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ActorId, x.ActorName, x.Event, x.FromValue, x.ToValue, x.CreatedAt }).ToListAsync(ct);
        return Ok(ApiResponse.Ok(events));
    }
}
