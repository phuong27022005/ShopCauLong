using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BadmintonShop.Tests;

public sealed class CatalogApiTests : IAsyncLifetime
{
    private readonly CatalogApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PublicCatalogSupportsPaginationAndDetailWithoutToken()
    {
        var page = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?page=2&pageSize=2"), HttpStatusCode.OK);
        page.Items.Select(x => x.Id).Should().Equal(3, 4);
        page.Items[0].BrandName.Should().Be("Yonex");
        page.Items[0].Stock.Should().Be(10);
        page.TotalCount.Should().Be(15);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);

        var newest = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?pageSize=2&newestFirst=true"), HttpStatusCode.OK);
        newest.Items.Select(x => x.Id).Should().Equal(15, 14);

        var detail = await ReadAsync<ProductDetailDto>(
            await _client.GetAsync($"/api/products/{page.Items[0].Slug}"), HttpStatusCode.OK);
        detail.Brand.Name.Should().Be("Yonex");
        detail.Category.Id.Should().Be(detail.CategoryId);

        (await ReadAsync<List<CategoryDto>>(await _client.GetAsync("/api/categories"), HttpStatusCode.OK))
            .Should().HaveCount(3);
        (await ReadAsync<List<BrandDto>>(await _client.GetAsync("/api/brands"), HttpStatusCode.OK))
            .Should().HaveCount(5);

        await AssertErrorAsync(await _client.GetAsync("/api/products/khong-ton-tai"), HttpStatusCode.NotFound);
        await AssertErrorAsync(await _client.GetAsync("/api/products?page=0&pageSize=101"), HttpStatusCode.BadRequest);
    }

    public static TheoryData<string, string> AdminRoutes => new()
    {
        { "POST", "/api/products" }, { "PUT", "/api/products/1" }, { "DELETE", "/api/products/1" },
        { "POST", "/api/categories" }, { "PUT", "/api/categories/1" }, { "DELETE", "/api/categories/1" },
        { "POST", "/api/brands" }, { "PUT", "/api/brands/1" }, { "DELETE", "/api/brands/1" },
        { "POST", "/api/products/1/image" }
    };

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task WriteEndpointsRequireAdmin(string method, string url)
    {
        using var anonymous = new HttpRequestMessage(new HttpMethod(method), url);
        await AssertErrorAsync(await _client.SendAsync(anonymous), HttpStatusCode.Unauthorized);
        using var customer = new HttpRequestMessage(new HttpMethod(method), url);
        customer.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Customer"));
        await AssertErrorAsync(await _client.SendAsync(customer), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ForgedAdminTokenIsRejected()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CatalogApiFactory.Token("Admin", "another-test-key-that-is-long-enough-for-hs256"));
        await AssertErrorAsync(await _client.DeleteAsync("/api/products/1"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminCanManageBrandsCategoriesAndProducts()
    {
        AsAdmin();
        var brand = await ReadAsync<BrandDto>(
            await _client.PostAsJsonAsync("/api/brands", new SaveBrandRequest("Mẫu", null)), HttpStatusCode.Created);
        var category = await ReadAsync<CategoryDto>(
            await _client.PostAsJsonAsync("/api/categories", new SaveCategoryRequest("Vợt thử")), HttpStatusCode.Created);
        var duplicateCategory = await ReadAsync<CategoryDto>(
            await _client.PostAsJsonAsync("/api/categories", new SaveCategoryRequest("Vợt thử")), HttpStatusCode.Created);
        duplicateCategory.Slug.Should().Be("vot-thu-2");

        (await ReadAsync<BrandDto>(
            await _client.PutAsJsonAsync($"/api/brands/{brand.Id}", new SaveBrandRequest("Mẫu mới", null)),
            HttpStatusCode.OK)).Name.Should().Be("Mẫu mới");
        (await ReadAsync<CategoryDto>(
            await _client.PutAsJsonAsync($"/api/categories/{category.Id}", new SaveCategoryRequest("Vợt thử mới")),
            HttpStatusCode.OK)).Slug.Should().Be("vot-thu-moi");

        var response = await _client.PostAsJsonAsync("/api/products", Product("Vợt Đập", brand.Id, category.Id));
        response.Headers.Location!.AbsolutePath.Should().Be("/api/products/vot-dap");
        var product = await ReadAsync<ProductDetailDto>(response, HttpStatusCode.Created);
        var duplicateProduct = await ReadAsync<ProductDetailDto>(
            await _client.PostAsJsonAsync("/api/products", Product("Vợt Đập", 1, 1)), HttpStatusCode.Created);
        duplicateProduct.Slug.Should().Be("vot-dap-2");

        var updated = await ReadAsync<ProductDetailDto>(
            await _client.PutAsJsonAsync($"/api/products/{product.Id}", Product("Vợt Đập Mới", 1, 1, weight: "2U")),
            HttpStatusCode.OK);
        updated.Slug.Should().Be("vot-dap-moi");
        updated.Weight.Should().Be("2U");
        updated.Brand.Id.Should().Be(1);
        updated.Category.Id.Should().Be(1);
        await AssertErrorAsync(await _client.GetAsync("/api/products/vot-dap"), HttpStatusCode.NotFound);
        await ReadAsync<object>(await _client.DeleteAsync($"/api/products/{product.Id}"), HttpStatusCode.OK);
        await ReadAsync<object>(await _client.DeleteAsync($"/api/categories/{category.Id}"), HttpStatusCode.OK);
        await ReadAsync<object>(await _client.DeleteAsync($"/api/brands/{brand.Id}"), HttpStatusCode.OK);
        await AssertErrorAsync(await _client.DeleteAsync("/api/brands/1"), HttpStatusCode.Conflict);
        await AssertErrorAsync(await _client.DeleteAsync("/api/categories/1"), HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("Vợt", -100, null)]
    [InlineData("Vợt", 100, 100)]
    [InlineData("Vợt", 100, 101)]
    [InlineData(null, 100, null)]
    public async Task ProductWritesRejectInvalidData(string? name, int price, int? discount)
    {
        AsAdmin();
        var input = new { name, price, discountPrice = discount, stock = 0, brandId = 1, categoryId = 1, weight = "F" };
        await AssertErrorAsync(await _client.PostAsJsonAsync("/api/products", input), HttpStatusCode.BadRequest);
        await AssertErrorAsync(await _client.PutAsJsonAsync("/api/products/1", input), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CatalogWritesHandleMissingIdsAndRequiredNames()
    {
        AsAdmin();
        await AssertErrorAsync(await _client.PutAsJsonAsync("/api/products/999", Product("Vợt", 1, 1)),
            HttpStatusCode.NotFound);
        await AssertErrorAsync(await _client.DeleteAsync("/api/products/999"), HttpStatusCode.NotFound);
        await AssertErrorAsync(await _client.PostAsJsonAsync("/api/products", Product("Vợt", 999, 1)),
            HttpStatusCode.BadRequest);
        await AssertErrorAsync(await _client.PostAsJsonAsync("/api/categories", new { name = "" }),
            HttpStatusCode.BadRequest);
        await AssertErrorAsync(await _client.PutAsJsonAsync("/api/categories/999", new { name = "Vợt" }),
            HttpStatusCode.NotFound);
        await AssertErrorAsync(await _client.DeleteAsync("/api/categories/999"), HttpStatusCode.NotFound);
        await AssertErrorAsync(await _client.PostAsJsonAsync("/api/brands", new { name = "" }),
            HttpStatusCode.BadRequest);
        await AssertErrorAsync(await _client.PutAsJsonAsync("/api/brands/999", new { name = "Vợt" }),
            HttpStatusCode.NotFound);
        await AssertErrorAsync(await _client.DeleteAsync("/api/brands/999"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("image.gif", 100)]
    [InlineData("image.png", 100)]
    [InlineData("image.png", 2 * 1024 * 1024 + 1)]
    public async Task UploadRejectsUnsupportedTypeBadSignatureAndOversizedFile(string fileName, int size)
    {
        AsAdmin();
        var bytes = new byte[size];
        if (size > 100) Png.CopyTo(bytes, 0);
        using var content = ImageForm(fileName, bytes);

        await AssertErrorAsync(await _client.PostAsync("/api/products/1/image", content), HttpStatusCode.BadRequest);
        Directory.EnumerateFiles(Path.Combine(_factory.WebRoot, "uploads")).Should().BeEmpty();
    }

    [Fact]
    public async Task AdminCanUploadGuidNamedImageAndPublicCanReadIt()
    {
        AsAdmin();
        using var content = ImageForm("original.png", Png);
        var data = await ReadAsync<JsonElement>(
            await _client.PostAsync("/api/products/1/image", content), HttpStatusCode.OK);
        var url = data.GetProperty("imageUrl").GetString()!;
        url.Should().MatchRegex("^/uploads/[a-f0-9]{32}\\.png$");

        _client.DefaultRequestHeaders.Authorization = null;
        using var image = await _client.GetAsync(url);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        image.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(Png);

        var product = await ReadAsync<ProductDetailDto>(
            await _client.GetAsync("/api/products/yonex-astrox-88d"), HttpStatusCode.OK);
        product.ImageUrl.Should().Be(url);
    }

    [Fact]
    public async Task SoftDeletedProductIsHiddenButOldOrderStillHasItsProductAndPrice()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var order = new Order
            {
                UserId = 2, TotalAmount = 750_000m, ShippingAddress = "Hà Nội", Phone = "0900000000",
                Status = "Paid", PaymentMethod = "COD", CreatedAt = DateTime.UtcNow
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            db.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = 1, Quantity = 1, UnitPrice = 750_000m });
            await db.SaveChangesAsync();
        }

        AsAdmin();
        await ReadAsync<object>(await _client.DeleteAsync("/api/products/1"), HttpStatusCode.OK);
        _client.DefaultRequestHeaders.Authorization = null;
        var page = await ReadAsync<PageDto<ProductListDto>>(await _client.GetAsync("/api/products"), HttpStatusCode.OK);
        page.TotalCount.Should().Be(14);
        page.Items.Should().NotContain(x => x.Id == 1);
        await AssertErrorAsync(await _client.GetAsync("/api/products/yonex-astrox-88d"), HttpStatusCode.NotFound);

        // Đọc lịch sử trực tiếp vì bước này chưa cung cấp endpoint đơn hàng.
        await using var read = _factory.Services.CreateAsyncScope();
        var database = read.ServiceProvider.GetRequiredService<ShopDbContext>();
        var historical = await (
            from item in database.OrderItems
            join product in database.Products on item.ProductId equals product.Id
            select new { product.Name, product.IsActive, item.UnitPrice }).SingleAsync();
        historical.Name.Should().Be("Yonex Astrox 88D");
        historical.IsActive.Should().BeFalse();
        historical.UnitPrice.Should().Be(750_000m);
    }


    [Theory]
    [InlineData("search=yonex", 3)]
    [InlineData("search=YONEX", 3)]
    [InlineData("search=khong-co", 0)]
    [InlineData("brandId=1&brandId=2", 6)]
    [InlineData("categoryId=2", 5)]
    [InlineData("weight=3U&weight=4U", 10)]
    [InlineData("stiffness=Cứng", 5)]
    [InlineData("balancePoint=Cân bằng", 5)]
    [InlineData("inStock=true", 15)]
    [InlineData("minPrice=1090000&maxPrice=1230000", 2)]
    [InlineData("brandId=1&minPrice=1000000&maxPrice=1200000&weight=4U", 1)]
    public async Task FiltersReturnExpectedCounts(string query, int count)
    {
        var result = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?" + query + "&pageSize=50"), HttpStatusCode.OK);
        result.TotalItems.Should().Be(count);
        result.Items.Should().HaveCount(count);
    }

    [Theory]
    [InlineData("pageSize=51")]
    [InlineData("minPrice=100&maxPrice=10")]
    [InlineData("sortBy=unknown")]
    [InlineData("sortDir=unknown")]
    [InlineData("weight=6U")]
    [InlineData("brandId=-1")]
    public async Task InvalidFiltersReturn400(string query) =>
        await AssertErrorAsync(await _client.GetAsync("/api/products?" + query), HttpStatusCode.BadRequest);

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task SortAndPaginationRunAtDatabase(string direction)
    {
        var page = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?sortBy=price&sortDir=" + direction), HttpStatusCode.OK);
        page.TotalPages.Should().Be(2);
        page.Items.Should().HaveCount(12);
        var prices = page.Items.Select(x => x.Price);
        if (direction == "asc") prices.Should().BeInAscendingOrder();
        else prices.Should().BeInDescendingOrder();
        var empty = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?page=2147483647"), HttpStatusCode.OK);
        empty.Items.Should().BeEmpty();
        empty.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task SearchHandlesVietnameseAndAlwaysExcludesInactiveProducts()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var products = await db.Products.OrderBy(x => x.Id).Take(2).ToListAsync();
            products[0].Name = "Vợt Đỏ Đẹp";
            products[0].Stock = 0;
            products[1].Name = "Vợt Đỏ Đẹp";
            products[1].IsActive = false;
            await db.SaveChangesAsync();
        }
        var result = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?search=VOT%20DO"), HttpStatusCode.OK);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(1);
        var available = await ReadAsync<PageDto<ProductListDto>>(
            await _client.GetAsync("/api/products?search=vot&inStock=true"), HttpStatusCode.OK);
        available.Items.Should().BeEmpty();
    }

    private void AsAdmin() =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Admin"));

    private static SaveProductRequest Product(string name, int brand, int category, string weight = "F") => new()
    {
        Name = name, BrandId = brand, CategoryId = category, Weight = weight, Price = 500_000m, Stock = 2
    };

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jY8sAAAAASUVORK5CYII=");

    private static MultipartFormDataContent ImageForm(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "file", fileName);
        return content;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(status, "nội dung phản hồi là {0}", body);
            var envelope = JsonSerializer.Deserialize<Envelope<T>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            envelope!.Success.Should().BeTrue();
            envelope.Message.Should().NotBeNullOrWhiteSpace();
            return envelope.Data!;
        }
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(status, "nội dung phản hồi là {0}", body);
            using var envelope = JsonDocument.Parse(body);
            envelope.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            envelope.RootElement.TryGetProperty("data", out _).Should().BeTrue();
            envelope.RootElement.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    private sealed record Envelope<T>(bool Success, T? Data, string Message);
}
