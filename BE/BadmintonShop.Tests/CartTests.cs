using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BadmintonShop.Api.Cart;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BadmintonShop.Tests;

public sealed class CartTests : IAsyncLifetime
{
    private readonly CatalogApiFactory factory = new();
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreatedAsync();
        AsUser("2");
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private void AsUser(string id) => client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Customer", userId: id));
    private async Task<CartDto> Read(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var envelope = await response.Content.ReadFromJsonAsync<Envelope>();
            envelope!.Success.Should().BeTrue();
            return envelope.Data;
        }
    }
    private sealed record Envelope(bool Success, CartDto Data, string Message);
    private Task<HttpResponseMessage> Add(int id, int quantity) =>
        client.PostAsJsonAsync("/api/cart/items", new { productId = id, quantity, price = 1, userId = 1 });

    [Theory]
    [InlineData("GET", "/api/cart")]
    [InlineData("POST", "/api/cart/items")]
    [InlineData("PUT", "/api/cart/items/1")]
    [InlineData("DELETE", "/api/cart/items/1")]
    [InlineData("DELETE", "/api/cart")]
    [InlineData("POST", "/api/cart/merge")]
    public async Task AnonymousRequestsAreRejected(string method, string path)
    {
        client.DefaultRequestHeaders.Authorization = null;
        (await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddAccumulatesUpdateReplacesAndDeleteClears()
    {
        (await Read(await Add(1, 2))).Items.Single().Quantity.Should().Be(2);
        var added = await Read(await Add(1, 3));
        added.Items.Single().Quantity.Should().Be(5);
        added.Items.Single().UnitPrice.Should().Be(950000);
        (await Read(await client.PutAsJsonAsync("/api/cart/items/1", new { quantity = 4 })))
            .Items.Single().Quantity.Should().Be(4);
        (await Read(await client.DeleteAsync("/api/cart/items/1"))).Items.Should().BeEmpty();
        await Read(await Add(1, 1));
        await Read(await Add(2, 1));
        (await Read(await client.DeleteAsync("/api/cart"))).Items.Should().BeEmpty();
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Carts.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task InvalidQuantitiesAreRejected(int quantity) =>
        (await Add(1, quantity)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task OwnershipIsTakenOnlyFromToken()
    {
        await Read(await Add(1, 2));
        AsUser("1");
        (await Read(await client.GetAsync("/api/cart"))).Items.Should().BeEmpty();
        (await client.PutAsJsonAsync("/api/cart/items/1", new { quantity = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync("/api/cart/items/1")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await Read(await client.DeleteAsync("/api/cart"));
        AsUser("2");
        (await Read(await client.GetAsync("/api/cart"))).Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task MergeCombinesDuplicateAndNewProductsAndRollsBackInvalidBatch()
    {
        await Read(await Add(1, 2));
        var merged = await Read(await client.PostAsJsonAsync("/api/cart/merge",
            new { items = new[] { new { productId = 1, quantity = 1 }, new { productId = 1, quantity = 2 }, new { productId = 2, quantity = 3 } } }));
        merged.Items.Single(x => x.ProductId == 1).Quantity.Should().Be(5);
        merged.Items.Single(x => x.ProductId == 2).Quantity.Should().Be(3);

        (await client.PostAsJsonAsync("/api/cart/merge", new { items = new[] {
            new { productId = 3, quantity = 1 }, new { productId = 1, quantity = 6 } } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var after = await Read(await client.GetAsync("/api/cart"));
        after.Items.Should().HaveCount(2);
        after.Items.Single(x => x.ProductId == 1).Quantity.Should().Be(5);
    }

    [Fact]
    public async Task HiddenMissingAndInsufficientStockAreRejected()
    {
        (await Add(999, 1)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            (await db.Products.FindAsync(1))!.IsActive = false;
            (await db.Products.FindAsync(2))!.Stock = 0;
            (await db.Products.FindAsync(3))!.Stock = 2;
            await db.SaveChangesAsync();
        }
        (await Add(1, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Add(2, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Add(3, 3)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Read(await Add(3, 2));
        (await Add(3, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CartReflectsAdminPriceChangesAndUnavailableProducts()
    {
        await Read(await Add(1, 2));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Admin"));
        var response = await client.PutAsJsonAsync("/api/products/1", new SaveProductRequest
        {
            Name = "Vợt mới", Price = 1000, DiscountPrice = 800, Stock = 10,
            BrandId = 1, CategoryId = 1, Weight = "4U"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AsUser("2");
        (await Read(await client.GetAsync("/api/cart"))).TotalAmount.Should().Be(1600);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var product = (await db.Products.FindAsync(1))!;
            product.IsActive = false;
            product.Stock = 0;
            await db.SaveChangesAsync();
        }
        var cart = await Read(await client.GetAsync("/api/cart"));
        cart.Items.Single().IsAvailable.Should().BeFalse();
        cart.Items.Single().Product.Name.Should().Be("Vợt mới");
    }

    [Theory]
    [InlineData("not-an-id")]
    [InlineData("999")]
    public async Task InvalidUserIdentityIsRejected(string id)
    {
        AsUser(id);
        (await client.GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void CalculationUsesCurrentDiscountAndAvailability()
    {
        var result = CartService.Calculate(
            [new CartItem { ProductId = 1, Quantity = 2 }, new CartItem { ProductId = 2, Quantity = 3 }],
            [new Product { Id = 1, Price = 100, DiscountPrice = 80, Stock = 2, IsActive = true },
             new Product { Id = 2, Price = 50, Stock = 0, IsActive = true }]);
        result.TotalAmount.Should().Be(310);
        result.Items[0].IsAvailable.Should().BeTrue();
        result.Items[1].IsAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(11, 20)]
    [InlineData(3, 2)]
    public void QuantityRulesRejectInvalidTotals(int quantity, int stock)
    {
        var action = () => CartService.ValidateQuantity(new Product { IsActive = true, Stock = stock }, quantity);
        action.Should().Throw<CatalogException>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ServiceAccumulatesExistingQuantity()
    {
        var repository = new Mock<ICartRepository>();
        var cart = new Cart { Id = 1, UserId = 2 };
        var items = new List<CartItem> { new() { ProductId = 1, Quantity = 2 } };
        repository.Setup(x => x.WithCartAsync(2, It.IsAny<Func<Cart, List<CartItem>, Task<CartDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((int _, Func<Cart, List<CartItem>, Task<CartDto>> action, CancellationToken _) => action(cart, items));
        repository.Setup(x => x.ProductsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Product { Id = 1, IsActive = true, Stock = 10, Price = 100 }]);
        var result = await new CartService(repository.Object).AddAsync(2, new AddCartItemRequest(1, 3), default);
        result.Items.Single().Quantity.Should().Be(5);
        repository.Verify(x => x.Add(It.IsAny<CartItem>()), Times.Never);
    }
}

