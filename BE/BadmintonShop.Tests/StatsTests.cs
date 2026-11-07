using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BadmintonShop.Api.Stats;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BadmintonShop.Tests;

public sealed class StatsTests : IAsyncLifetime
{
    private readonly CatalogApiFactory factory = new();
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Admin"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        await db.Database.EnsureCreatedAsync();
        foreach (var user in await db.Users.ToListAsync()) user.CreatedAt = new DateTime(2026, 1, 1);
        foreach (var product in await db.Products.ToListAsync()) product.Stock = 20;
        (await db.Products.FindAsync(1))!.Stock = 5;
        (await db.Products.FindAsync(2))!.Stock = 0;
        var hidden = (await db.Products.FindAsync(3))!;
        hidden.Stock = 1;
        hidden.IsActive = false;
        await db.SaveChangesAsync();
        await Add(db, "Completed", "Unpaid", 100.29m, new(2026, 1, 1), (1, 2));
        await Add(db, "Pending", "Paid", 200.11m, new(2026, 1, 1, 23, 59, 59), (2, 3));
        await Add(db, "Completed", "Paid", 50.60m, new(2026, 1, 31, 23, 59, 59), (1, 1), (2, 1));
        await Add(db, "Cancelled", "Paid", 999m, new(2026, 1, 10), (1, 20));
        await Add(db, "Pending", "Unpaid", 888m, new(2026, 1, 11), (2, 20));
        await Add(db, "Completed", "Unpaid", 400m, new(2026, 2, 1), (1, 5));
    }
    private static async Task Add(ShopDbContext db, string status, string payment, decimal total, DateTime date,
        params (int Product, int Quantity)[] lines)
    {
        var order = new Order { UserId = 2, Status = status, PaymentStatus = payment, TotalAmount = total,
            PaymentMethod = "COD", CreatedAt = date };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        foreach (var line in lines)
            db.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = line.Product, ProductName = "Tên lúc mua",
                Quantity = line.Quantity, UnitPrice = 9999 });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task<T> Get<T>(string path)
    {
        using var response = await client.GetAsync("/api/admin/stats/" + path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        return json.GetProperty("data").Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public async Task OverviewMatchesManualTotalsAndExcludesCancelledPaidOrders()
    {
        var result = await Get<StatsOverview>("overview?from=2026-01-01&to=2026-01-31");
        result.TotalRevenue.Should().Be(351m);
        result.OrdersByStatus.Should().BeEquivalentTo(new[] {
            new OrderStatusCount("Completed", 2), new OrderStatusCount("Pending", 2), new OrderStatusCount("Cancelled", 1) });
        result.NewCustomers.Should().Be(1);
        result.LowStockProducts.Should().Be(2);
        (await Get<StatsOverview>("overview")).TotalRevenue.Should().Be(751m);
    }

    [Fact]
    public async Task RevenueGroupsAtDatabaseByDayAndMonthWithInclusiveEndDate()
    {
        (await Get<List<RevenuePoint>>("revenue?from=2026-01-01&to=2026-01-31")).Should().Equal(
            new RevenuePoint(new(2026, 1, 1), 300.4m), new RevenuePoint(new(2026, 1, 31), 50.6m));
        (await Get<List<RevenuePoint>>("revenue?groupBy=month")).Should().Equal(
            new RevenuePoint(new(2026, 1, 1), 351m), new RevenuePoint(new(2026, 2, 1), 400m));
        (await Get<List<RevenuePoint>>("revenue?from=2026-02-01")).Should().ContainSingle()
            .Which.Revenue.Should().Be(400m);
        (await Get<List<RevenuePoint>>("revenue?to=2026-01-01")).Should().ContainSingle()
            .Which.Revenue.Should().Be(300.4m);
    }

    [Fact]
    public async Task TopProductsCountsOnlyQualifyingOrdersAndAppliesLimit()
    {
        var all = await Get<List<TopProduct>>("top-products");
        all.Select(x => (x.ProductId, x.QuantitySold)).Should().Equal((1, 8L), (2, 4L));
        (await Get<List<TopProduct>>("top-products?limit=1")).Should().ContainSingle().Which.ProductId.Should().Be(1);
    }

    [Fact]
    public async Task EmptyRangeReturnsZeroAndEmptySeriesWhileStockRemainsCurrent()
    {
        var result = await Get<StatsOverview>("overview?from=2030-01-01&to=2030-01-31");
        result.TotalRevenue.Should().Be(0);
        result.OrdersByStatus.Should().BeEmpty();
        result.NewCustomers.Should().Be(0);
        result.LowStockProducts.Should().Be(2);
        (await Get<List<RevenuePoint>>("revenue?from=2030-01-01&to=2030-01-31")).Should().BeEmpty();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        await db.OrderItems.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        (await Get<List<TopProduct>>("top-products")).Should().BeEmpty();
    }

    [Theory]
    [InlineData("overview?from=2026-02-01&to=2026-01-01")]
    [InlineData("revenue?from=2026-02-01&to=2026-01-01")]
    [InlineData("revenue?groupBy=year")]
    [InlineData("overview?from=bad-date")]
    [InlineData("top-products?limit=0")]
    [InlineData("top-products?limit=101")]
    public async Task InvalidQueriesReturn400(string path)
    {
        using var response = await client.GetAsync("/api/admin/stats/" + path);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("revenue")]
    [InlineData("top-products")]
    public async Task EveryEndpointRequiresAdmin(string path)
    {
        client.DefaultRequestHeaders.Authorization = null;
        (await client.GetAsync("/api/admin/stats/" + path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Customer", userId: "2"));
        (await client.GetAsync("/api/admin/stats/" + path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void ValidatorsAcceptBoundariesAndRejectReversedDates()
    {
        var validator = new StatsQueryValidator();
        validator.Validate(new StatsQuery { From = new(2026, 1, 1), To = new(2026, 1, 1) }).IsValid.Should().BeTrue();
        validator.Validate(new StatsQuery { From = new(2026, 2, 1), To = new(2026, 1, 1) }).IsValid.Should().BeFalse();
        new TopProductsQueryValidator().Validate(new TopProductsQuery { Limit = 100 }).IsValid.Should().BeTrue();
    }
}

