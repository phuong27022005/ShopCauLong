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

public sealed class OrderTests : IAsyncLifetime
{
    private readonly CatalogApiFactory factory = new(concurrent: true);
    private HttpClient client = null!;
    private static readonly CreateOrderRequest Input = new("Hà Nội", "0900000000", "COD", "Giao buổi sáng", "Nguyễn An");
    public async Task InitializeAsync()
    {
        client = Client("2");
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreatedAsync();
    }
    private HttpClient Client(string user, string role = "Customer")
    {
        var result = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        result.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token(role, userId: user));
        return result;
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private static async Task<T> Data<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        using (response)
        {
            response.StatusCode.Should().Be(status, await response.Content.ReadAsStringAsync());
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("data").Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
    }
    private async Task Add(int id = 1, int quantity = 2)
    {
        (await client.PostAsJsonAsync("/api/cart/items", new { productId = id, quantity })).StatusCode.Should().Be(HttpStatusCode.OK);
    }
    private async Task<OrderDto> Create()
    {
        await Add();
        return await Data<OrderDto>(await client.PostAsJsonAsync("/api/orders", Input), HttpStatusCode.Created);
    }

    [Fact]
    public void TotalsAndEveryStatusPairAreCorrect()
    {
        OrderRules.Total([new OrderItem { Quantity = 2, UnitPrice = 800 }, new OrderItem { Quantity = 3, UnitPrice = 50 }]).Should().Be(1750);
        var allowed = new[] { ("Pending","Confirmed"), ("Confirmed","Shipping"), ("Shipping","Completed"),
            ("Pending","Cancelled"), ("Confirmed","Cancelled") };
        foreach (var from in OrderRules.Statuses)
            foreach (var to in OrderRules.Statuses)
                OrderRules.CanTransition(from, to).Should().Be(allowed.Contains((from,to)));
    }

    [Fact]
    public async Task CreateSnapshotsPricesClearsCartAndSubtractsStock()
    {
        await Add();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            (await db.Products.FindAsync(1))!.DiscountPrice = 800000;
            await db.SaveChangesAsync();
        }
        var order = await Data<OrderDto>(await client.PostAsJsonAsync("/api/orders", Input), HttpStatusCode.Created);
        order.Code.Should().MatchRegex("^BS[0-9]{8}-[0-9]{4,}$");
        order.TotalAmount.Should().Be(1600000);
        order.FullName.Should().Be("Nguyễn An");
        order.Items.Single().UnitPrice.Should().Be(800000);
        (await Data<CartDto>(await client.GetAsync("/api/cart"))).Items.Should().BeEmpty();
        using var admin = Client("1","Admin");
        (await admin.PutAsJsonAsync("/api/products/1", new SaveProductRequest {
            Name = "Đổi giá", Price = 2000000, BrandId = 1, CategoryId = 1, Weight = "4U", Stock = 8
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var historical = await Data<OrderDto>(await client.GetAsync("/api/orders/" + order.Id));
        historical.Items.Single().UnitPrice.Should().Be(800000);
        historical.FullName.Should().Be("Nguyễn An");
        historical.Items.Single().ProductName.Should().Be("Yonex Astrox 88D");
        await using var read = factory.Services.CreateAsyncScope();
        (await read.ServiceProvider.GetRequiredService<ShopDbContext>().Products.FindAsync(1))!.Stock.Should().Be(8);
    }

    [Fact]
    public async Task EmptyAndUnavailableCartRollBack()
    {
        (await client.PostAsJsonAsync("/api/orders", Input)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Add(1);
        await Add(2);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            (await db.Products.FindAsync(2))!.Stock = 0;
            await db.SaveChangesAsync();
        }
        (await client.PostAsJsonAsync("/api/orders", Input)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var read = factory.Services.CreateAsyncScope();
        var database = read.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await database.Products.FindAsync(1))!.Stock.Should().Be(10);
        (await database.Orders.CountAsync()).Should().Be(0);
        (await database.CartItems.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CancellationRestoresStockOnceAndShippingCannotBeCancelled()
    {
        var order = await Create();
        await Data<OrderDto>(await client.PostAsync($"/api/orders/{order.Id}/cancel", null));
        (await client.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using (var scope = factory.Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Products.FindAsync(1))!.Stock.Should().Be(10);
        var shipping = await Create();
        using var admin = Client("1","Admin");
        (await admin.PutAsJsonAsync($"/api/admin/orders/{shipping.Id}/status", new { status = "Completed" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach (var status in new[] { "Confirmed", "Shipping" })
            await Data<OrderDto>(await admin.PutAsJsonAsync($"/api/admin/orders/{shipping.Id}/status", new { status }));
        (await client.PostAsync($"/api/orders/{shipping.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Data<OrderDto>(await admin.PutAsJsonAsync($"/api/admin/orders/{shipping.Id}/status", new { status = "Completed" }));
    }

    [Fact]
    public async Task OwnershipAdminAuthorizationAndFilters()
    {
        var order = await Create();
        using var other = Client("1");
        (await other.GetAsync("/api/orders/" + order.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Data<PageDto<OrderDto>>(await other.GetAsync("/api/orders"))).TotalItems.Should().Be(0);
        (await client.GetAsync("/api/admin/orders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = "Confirmed" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var admin = Client("1","Admin");
        var page = await Data<PageDto<OrderDto>>(await admin.GetAsync("/api/admin/orders?status=Pending&search=" + order.Code));
        page.TotalItems.Should().Be(1);
        (await Data<PageDto<OrderDto>>(await admin.GetAsync("/api/admin/orders?search=0900000000"))).TotalItems.Should().Be(1);
        (await Data<PageDto<OrderDto>>(await admin.GetAsync("/api/admin/orders?fromDate=2000-01-01&toDate=2000-01-02"))).Items.Should().BeEmpty();
        (await Data<PageDto<OrderDto>>(await client.GetAsync("/api/orders?page=99"))).Items.Should().BeEmpty();
        client.DefaultRequestHeaders.Authorization = null;
        (await client.GetAsync("/api/orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExactlyOneConcurrentCheckoutWinsLastItem()
    {
        await Add(1,1);
        using var other = Client("1");
        (await other.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            (await db.Products.FindAsync(1))!.Stock = 1;
            await db.SaveChangesAsync();
        }
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/orders", Input), other.PostAsJsonAsync("/api/orders", Input));
        responses.Count(x => x.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(x => x.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict).Should().Be(1);
        foreach (var response in responses) response.Dispose();
        await using var read = factory.Services.CreateAsyncScope();
        var database = read.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await database.Products.FindAsync(1))!.Stock.Should().Be(0);
        (await database.Orders.CountAsync()).Should().Be(1);
        (await database.CartItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RowVersionRejectsStaleProductWrite()
    {
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<ShopDbContext>();
        var b = second.ServiceProvider.GetRequiredService<ShopDbContext>();
        var old = (await b.Products.FindAsync(1))!;
        (await a.Products.FindAsync(1))!.Stock = 9;
        await a.SaveChangesAsync();
        old.Stock = 8;
        var action = () => b.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task FailureAfterFirstSaveRollsBackEverything()
    {
        await using var failing = new CatalogApiFactory(concurrent: true) { SaveInterceptor = new FailOrderLines() };
        using var http = failing.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Customer", userId: "2"));
        await using (var scope = failing.Services.CreateAsyncScope())
            {
                var setup = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
                await setup.Database.EnsureCreatedAsync();
                setup.Coupons.Add(new Coupon { Code = "ROLLBACK", Type = "Fixed", Value = 100,
                    UsageLimit = 1, IsActive = true, StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow.AddDays(1) });
                await setup.SaveChangesAsync();
            }
        (await http.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 2 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.PostAsJsonAsync("/api/orders", Input with { CouponCode = "rollback" })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using var read = failing.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await db.Products.FindAsync(1))!.Stock.Should().Be(10);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.OrderItems.CountAsync()).Should().Be(0);
        (await db.CartItems.SingleAsync()).Quantity.Should().Be(2);
        (await db.Coupons.SingleAsync()).UsedCount.Should().Be(0);
    }

    private sealed class FailOrderLines : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<OrderItem>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Lỗi ghi dòng đơn hàng được tạo trong test.");
            return ValueTask.FromResult(result);
        }
    }
}

