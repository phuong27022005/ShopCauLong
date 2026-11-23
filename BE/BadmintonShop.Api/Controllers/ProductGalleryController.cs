using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Api.Controllers;

public sealed record GallerySelection(string Url);

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/products/{id:int}/gallery")]
public sealed class ProductGalleryController(ShopDbContext db, ProductImageStore images) : ControllerBase
{
    private async Task<Product> Product(int id, CancellationToken ct) =>
        await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw CatalogException.NotFound("Sản phẩm");
    private static object Result(Product p) => new { p.ImageUrl, p.AdditionalImageUrls };

    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(ApiResponse.Ok(Result(await Product(id, ct))));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024 + 65536)]
    public async Task<IActionResult> Upload(int id, IFormFile? file, CancellationToken ct)
    {
        var product = await Product(id, ct);
        if (product.AdditionalImageUrls.Count + (product.ImageUrl is null ? 0 : 1) >= 8)
            throw CatalogException.BadRequest("Mỗi sản phẩm có tối đa 8 ảnh.");
        var url = await images.SaveAsync(file, ct);
        try
        {
            if (product.ImageUrl is null) product.ImageUrl = url;
            else product.AdditionalImageUrls.Add(url);
            await db.SaveChangesAsync(ct);
        }
        catch { images.Delete(url); throw; }
        return Ok(ApiResponse.Ok(Result(product)));
    }

    [HttpPut("cover")]
    public async Task<IActionResult> SetCover(int id, GallerySelection input, CancellationToken ct)
    {
        var product = await Product(id, ct);
        if (product.ImageUrl == input.Url) return Ok(ApiResponse.Ok(Result(product)));
        if (!product.AdditionalImageUrls.Remove(input.Url)) throw CatalogException.BadRequest("Ảnh không thuộc sản phẩm.");
        if (product.ImageUrl is not null) product.AdditionalImageUrls.Insert(0, product.ImageUrl);
        product.ImageUrl = input.Url;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok(Result(product)));
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(int id, GallerySelection input, CancellationToken ct)
    {
        var product = await Product(id, ct);
        if (product.ImageUrl == input.Url)
        {
            product.ImageUrl = product.AdditionalImageUrls.FirstOrDefault();
            if (product.ImageUrl is not null) product.AdditionalImageUrls.RemoveAt(0);
        }
        else if (!product.AdditionalImageUrls.Remove(input.Url)) throw CatalogException.BadRequest("Ảnh không thuộc sản phẩm.");
        await db.SaveChangesAsync(ct);
        // Keep physical files: existing URLs may be shared by another product or cached clients.
        return Ok(ApiResponse.Ok(Result(product)));
    }
}
