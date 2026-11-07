using AutoMapper;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Catalog;

public sealed class CatalogService(ICatalogRepository repository, IMapper mapper, ProductImageStore images)
{
    public async Task<PageDto<ProductListDto>> ListProductsAsync(ProductPageQuery query, CancellationToken ct)
    {
        var page = await repository.ListProductsAsync(query, ct);
        return new PageDto<ProductListDto>(mapper.Map<List<ProductListDto>>(page.Items),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PageDto<ProductDetailDto>> ListAdminProductsAsync(ProductPageQuery query, CancellationToken ct)
    {
        var page = await repository.ListProductsAsync(query, ct, includeHidden: true);
        return new(mapper.Map<List<ProductDetailDto>>(page.Items), page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<ProductDetailDto> GetProductAsync(string slug, CancellationToken ct)
    {
        var product = await repository.FindPublicProductAsync(slug, ct)
            ?? throw CatalogException.NotFound("Sản phẩm");
        return mapper.Map<ProductDetailDto>(product);
    }

    public async Task<ProductDetailDto> SaveProductAsync(int? id, SaveProductRequest input, CancellationToken ct)
    {
        var product = id is int productId
            ? await FindAsync<Product>(productId, "Sản phẩm", ct)
            : new Product { CreatedAt = DateTime.UtcNow, IsActive = true };
        var brand = await repository.FindAsync<Brand>(input.BrandId, ct)
            ?? throw CatalogException.BadRequest("Thương hiệu không tồn tại.");
        var category = await repository.FindAsync<Category>(input.CategoryId, ct)
            ?? throw CatalogException.BadRequest("Danh mục không tồn tại.");
        var name = input.Name.Trim();
        var regenerateSlug = id is null || product.Name != name;

        product.Name = name;
        product.Description = input.Description;
        product.Price = input.Price;
        product.DiscountPrice = input.DiscountPrice;
        product.Stock = input.Stock;
        product.BrandId = brand.Id;
        product.CategoryId = category.Id;
        product.Brand = brand;
        product.Category = category;
        product.Weight = input.Weight;
        product.Stiffness = input.Stiffness.Trim();
        product.BalancePoint = input.BalancePoint.Trim();
        product.ImageUrl = input.ImageUrl ?? product.ImageUrl;
        product.IsActive = input.IsActive ?? product.IsActive;

        if (id is null) repository.Add(product);
        if (regenerateSlug)
        {
            await SaveWithSlugAsync(async () => product.Slug = await SlugGenerator.CreateAsync(
                name, "san-pham", slug => repository.ExistsAsync<Product>(x => x.Slug == slug && x.Id != product.Id, ct)), ct);
        }
        else await repository.SaveAsync(ct);

        return mapper.Map<ProductDetailDto>(product);
    }

    public async Task DeleteProductAsync(int id, CancellationToken ct)
    {
        var product = await FindAsync<Product>(id, "Sản phẩm", ct);
        // Giữ sản phẩm để các dòng đơn hàng cũ còn tham chiếu được.
        product.IsActive = false;
        await repository.SaveAsync(ct);
    }

    public async Task<string> UploadImageAsync(int id, IFormFile? file, CancellationToken ct)
    {
        var product = await FindAsync<Product>(id, "Sản phẩm", ct);
        var url = await images.SaveAsync(file, ct);
        try
        {
            product.ImageUrl = url;
            await repository.SaveAsync(ct);
        }
        catch
        {
            images.Delete(url);
            throw;
        }

        return url;
    }

    public async Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct) =>
        mapper.Map<List<BrandDto>>(await repository.ListAsync<Brand>(ct));

    public async Task<BrandDto> SaveBrandAsync(int? id, SaveBrandRequest input, CancellationToken ct)
    {
        var brand = id is int brandId ? await FindAsync<Brand>(brandId, "Thương hiệu", ct) : new Brand();
        brand.Name = input.Name.Trim();
        brand.Logo = input.Logo;
        if (id is null) repository.Add(brand);
        await repository.SaveAsync(ct);
        return mapper.Map<BrandDto>(brand);
    }

    public async Task DeleteBrandAsync(int id, CancellationToken ct)
    {
        var brand = await FindAsync<Brand>(id, "Thương hiệu", ct);
        if (await repository.ExistsAsync<Product>(x => x.BrandId == id, ct))
            throw CatalogException.Conflict("Thương hiệu đang có sản phẩm.");
        repository.Remove(brand);
        await repository.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        mapper.Map<List<CategoryDto>>(await repository.ListAsync<Category>(ct));

    public async Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryRequest input, CancellationToken ct)
    {
        var category = id is int categoryId ? await FindAsync<Category>(categoryId, "Danh mục", ct) : new Category();
        var name = input.Name.Trim();
        var regenerateSlug = id is null || category.Name != name;
        category.Name = name;

        if (id is null) repository.Add(category);
        if (regenerateSlug)
        {
            await SaveWithSlugAsync(async () => category.Slug = await SlugGenerator.CreateAsync(
                name, "danh-muc", slug => repository.ExistsAsync<Category>(x => x.Slug == slug && x.Id != category.Id, ct)), ct);
        }
        else await repository.SaveAsync(ct);

        return mapper.Map<CategoryDto>(category);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await FindAsync<Category>(id, "Danh mục", ct);
        if (await repository.ExistsAsync<Product>(x => x.CategoryId == id, ct))
            throw CatalogException.Conflict("Danh mục đang có sản phẩm.");
        repository.Remove(category);
        await repository.SaveAsync(ct);
    }

    private async Task<T> FindAsync<T>(int id, string resource, CancellationToken ct) where T : class =>
        await repository.FindAsync<T>(id, ct) ?? throw CatalogException.NotFound(resource);

    private async Task SaveWithSlugAsync(Func<Task> assignSlug, CancellationToken ct)
    {
        // Index unique xử lý trường hợp hai request chọn cùng slug một lúc.
        for (var attempt = 1; ; attempt++)
        {
            await assignSlug();
            try
            {
                await repository.SaveAsync(ct);
                return;
            }
            catch (CatalogSlugConflictException) when (attempt < 3)
            {
                // Tạo lại slug từ dữ liệu vừa được ghi bởi request kia.
            }
        }
    }
}
