using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BadmintonShop.Api.Payments;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BadmintonShop.Tests;

public sealed class EngagementTests : IAsyncLifetime
{
    private readonly CatalogApiFactory factory = new(concurrent: true);
    private HttpClient customer = null!;
    private HttpClient admin = null!;
    private static CouponRequest CouponInput => new("SALE", "Percent", 10, 0, 100000, 10,
        DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), true);
    private static CreateOrderRequest OrderInput => new("Hà Nội", "0900000000", "COD", null, "Nguyễn An", "sale");

    public async Task InitializeAsync()
    {
        customer = Client("2", "Customer");
        admin = Client("1", "Admin");
        await Db(db => db.Database.EnsureCreatedAsync());
    }
    public async Task DisposeAsync() { customer.Dispose(); admin.Dispose(); await factory.DisposeAsync(); }
    private HttpClient Client(string id, string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token(role, userId: id));
        return client;
    }
    private async Task Db(Func<ShopDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ShopDbContext>());
    }
    private static async Task<T> Data<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
                .Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
    }
    private async Task<CouponDto> CreateCoupon(CouponRequest? input = null) =>
        await Data<CouponDto>(await admin.PostAsJsonAsync("/api/admin/coupons", input ?? CouponInput), HttpStatusCode.Created);
    private static async Task Add(HttpClient client, int product = 1) =>
        (await client.PostAsJsonAsync("/api/cart/items", new { productId = product, quantity = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
    private async Task Purchase(int user = 2)
    {
        await Db(async db =>
        {
            var order = new Order { UserId = user, Status = "Completed", PaymentMethod = "COD", CreatedAt = DateTime.UtcNow };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            db.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = 1, ProductName = "Vợt", UnitPrice = 100, Quantity = 1 });
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task EligibilityTracksCompletedPurchaseAndExistingReview()
    {
        const string path = "/api/products/1/reviews/eligibility";
        (await Data<JsonElement>(await customer.GetAsync(path))).GetProperty("canReview").GetBoolean().Should().BeFalse();
        await Purchase();
        (await Data<JsonElement>(await customer.GetAsync(path))).GetProperty("canReview").GetBoolean().Should().BeTrue();
        await Data<ReviewDto>(await customer.PostAsJsonAsync("/api/products/1/reviews", new ReviewRequest(5, "Tốt")), HttpStatusCode.Created);
        (await Data<JsonElement>(await customer.GetAsync(path))).GetProperty("canReview").GetBoolean().Should().BeFalse();
        var page = await Data<PageDto<ReviewDto>>(await admin.GetAsync("/api/admin/reviews"));
        page.Items.Should().ContainSingle();
        (await customer.GetAsync("/api/admin/reviews")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/admin/reviews?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminCanReadHiddenProductsWhilePublicCannot()
    {
        await Db(async db => { (await db.Products.FindAsync(1))!.IsActive = false; await db.SaveChangesAsync(); });
        var page = await Data<PageDto<ProductDetailDto>>(await admin.GetAsync("/api/admin/products?search=Astrox%2088D"));
        page.Items.Should().ContainSingle().Which.IsActive.Should().BeFalse();
        page.Items.Single().Brand.Name.Should().Be("Yonex");
        var publicPage = await Data<PageDto<ProductListDto>>(await customer.GetAsync("/api/products?search=Astrox%2088D"));
        publicPage.Items.Should().BeEmpty();
        (await customer.GetAsync("/api/admin/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var guest = factory.CreateClient();
        (await guest.GetAsync("/api/admin/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.GetAsync("/api/admin/reviews")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.GetAsync("/api/products/1/reviews/eligibility")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Percent", 20, 1000, 100, 100)]
    [InlineData("Percent", 10, 1000, null, 100)]
    [InlineData("Fixed", 200, 1000, null, 200)]
    [InlineData("Fixed", 2000, 1000, null, 1000)]
    [InlineData("Fixed", 100, 0, null, 0)]
    public void DiscountIsCappedAndNeverExceedsSubtotal(string type, decimal value, decimal subtotal, int? cap, decimal expected) =>
        CouponRules.Discount(new Coupon { Type = type, Value = value, MaxDiscount = cap }, subtotal).Should().Be(expected);

    [Fact]
    public async Task ReviewsRequirePurchaseAndOwnershipAndAverageTracksEveryMutation()
    {
        const string url = "/api/products/1/reviews";
        (await customer.PostAsJsonAsync(url, new ReviewRequest(5, "Tốt"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await Purchase();
        var review = await Data<ReviewDto>(await customer.PostAsJsonAsync(url, new ReviewRequest(5, "Tốt")), HttpStatusCode.Created);
        (await customer.PostAsJsonAsync(url, new ReviewRequest(4, "Trùng"))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await Purchase(1);
        var other = await Data<ReviewDto>(await admin.PostAsJsonAsync(url, new ReviewRequest(3, "Ổn")), HttpStatusCode.Created);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var page = await Data<ReviewPage>(await anonymous.GetAsync(url + "?pageSize=1"));
        page.AverageRating.Should().Be(4);
        page.TotalItems.Should().Be(2);
        page.Items.Should().HaveCount(1);
        (await anonymous.PostAsJsonAsync(url, new ReviewRequest(4, "Test"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await customer.DeleteAsync(url + "/" + other.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await Data<ReviewDto>(await customer.PutAsJsonAsync(url + "/" + review.Id, new ReviewRequest(1, "Đổi")));
        (await Data<ReviewPage>(await anonymous.GetAsync(url))).AverageRating.Should().Be(2);
        (await admin.DeleteAsync(url + "/" + review.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Data<ReviewPage>(await anonymous.GetAsync(url))).AverageRating.Should().Be(3);
        (await admin.DeleteAsync(url + "/" + other.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Data<ReviewPage>(await anonymous.GetAsync(url))).AverageRating.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task InvalidRatingsReturn400(int rating)
    {
        await Purchase();
        (await customer.PostAsJsonAsync("/api/products/1/reviews", new ReviewRequest(rating, "Sai"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("used")]
    [InlineData("minimum")]
    [InlineData("disabled")]
    public async Task InvalidCouponStatesReturn400(string state)
    {
        var input = CouponInput;
        input = state switch {
            "expired" => input with { StartAt = DateTime.UtcNow.AddDays(-2), EndAt = DateTime.UtcNow.AddDays(-1) },
            "future" => input with { StartAt = DateTime.UtcNow.AddHours(1) },
            "minimum" => input with { MinOrderAmount = 2000 },
            "disabled" => input with { IsActive = false },
            _ => input
        };
        var coupon = await CreateCoupon(input);
        if (state == "used") await Db(async db => { (await db.Coupons.FindAsync(coupon.Id))!.UsedCount = input.UsageLimit; await db.SaveChangesAsync(); });
        (await customer.PostAsJsonAsync("/api/coupons/validate", new ValidateCouponRequest("sale", 1000))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminCrudAndCaseInsensitiveCodes()
    {
        (await customer.PostAsJsonAsync("/api/admin/coupons", CouponInput)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var coupon = await CreateCoupon();
        (await admin.PostAsJsonAsync("/api/admin/coupons", CouponInput with { Code = "sale" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var discount = await Data<JsonElement>(await customer.PostAsJsonAsync("/api/coupons/validate", new ValidateCouponRequest("sAlE", 1000)));
        discount.GetProperty("discountAmount").GetDecimal().Should().Be(100);
        (await Data<List<CouponDto>>(await admin.GetAsync("/api/admin/coupons"))).Should().HaveCount(1);
        var updated = await Data<CouponDto>(await admin.PutAsJsonAsync("/api/admin/coupons/" + coupon.Id, CouponInput with { Value = 20 }));
        updated.Value.Should().Be(20);
        (await admin.DeleteAsync("/api/admin/coupons/" + coupon.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/admin/coupons/" + coupon.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrdersUseServerSubtotalAndCancellationReleasesUsageExactlyOnce()
    {
        var coupon = await CreateCoupon();
        await Add(customer);
        var order = await Data<OrderDto>(await customer.PostAsJsonAsync("/api/orders", OrderInput), HttpStatusCode.Created);
        var subtotal = order.Items.Sum(x => x.UnitPrice * x.Quantity);
        order.DiscountAmount.Should().Be(Math.Min(subtotal * .1m, 100000));
        order.TotalAmount.Should().Be(subtotal - order.DiscountAmount);
        order.CouponCode.Should().Be("SALE");
        await Db(async db => (await db.Coupons.FindAsync(coupon.Id))!.UsedCount.Should().Be(1));
        (await customer.PostAsync("/api/orders/" + order.Id + "/cancel", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.PostAsync("/api/orders/" + order.Id + "/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Db(async db => (await db.Coupons.FindAsync(coupon.Id))!.UsedCount.Should().Be(0));
    }

    [Fact]
    public async Task OnlyOneConcurrentOrderCanUseLastCouponSlot()
    {
        var coupon = await CreateCoupon(CouponInput with { UsageLimit = 1 });
        using var second = Client("1", "Customer");
        await Add(customer, 1);
        await Add(second, 2);
        var responses = await Task.WhenAll(customer.PostAsJsonAsync("/api/orders", OrderInput), second.PostAsJsonAsync("/api/orders", OrderInput));
        responses.Count(x => x.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Single(x => x.StatusCode != HttpStatusCode.Created).StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        await Db(async db => {
            (await db.Coupons.FindAsync(coupon.Id))!.UsedCount.Should().Be(1);
            (await db.Orders.CountAsync()).Should().Be(1);
            (await db.CartItems.CountAsync()).Should().Be(1);
        });
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task ExpiredVnpayOrderReleasesCouponAndStockOnce()
    {
        var coupon = await CreateCoupon();
        await Add(customer);
        var order = await Data<OrderDto>(await customer.PostAsJsonAsync("/api/orders", OrderInput with { PaymentMethod = "VNPAY" }), HttpStatusCode.Created);
        await Db(async db => { (await db.Orders.FindAsync(order.Id))!.PaymentExpiredAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); });
        await using var scope = factory.Services.CreateAsyncScope();
        var expiration = scope.ServiceProvider.GetRequiredService<PaymentExpiration>();
        await expiration.RunAsync(default);
        await expiration.RunAsync(default);
        await Db(async db => {
            (await db.Coupons.FindAsync(coupon.Id))!.UsedCount.Should().Be(0);
            (await db.Orders.FindAsync(order.Id))!.Status.Should().Be("Cancelled");
        });
    }
}

