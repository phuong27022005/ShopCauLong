using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BadmintonShop.Api.Auth;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BadmintonShop.Tests;

public sealed class ShopFeatureTests : IAsyncLifetime
{
    private readonly CatalogApiFactory factory = new();
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private void As(string id = "2", string role = "Customer") => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token(role, userId: id));
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private Task<HttpResponseMessage> Register() => client.PostAsJsonAsync("/api/auth/register", new { fullName = "Test User", email = "feature@example.com", password = "StrongPass1!" });
    private async Task<string> ActionToken(string purpose)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var subject = purpose == "reset" ? "Đặt lại mật khẩu Badminton Shop" : "Xác minh email Badminton Shop";
        var mail = await db.OutgoingEmails.Where(x => x.Subject == subject).OrderByDescending(x => x.Id).FirstAsync();
        return mail.Body.Split("#token=")[1].Split('\n')[0];
    }

    [Fact]
    public async Task EmailVerificationIsHashedExpiringAndSingleUse()
    {
        (await Register()).StatusCode.Should().Be(HttpStatusCode.Created);
        var token = await ActionToken("verify");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var stored = await db.AccountActionTokens.SingleAsync();
            stored.TokenHash.Should().Be(AccountActions.Hash(token)).And.NotBe(token);
            stored.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        }
        (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var read = factory.Services.CreateAsyncScope();
        (await read.ServiceProvider.GetRequiredService<ShopDbContext>().Users.SingleAsync(x => x.Email == "feature@example.com")).EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task PasswordResetRevokesAccessAndRefreshTokens()
    {
        await Register();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "feature@example.com", password = "StrongPass1!" });
        var access = (await Data(login)).GetProperty("accessToken").GetString();
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "feature@example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await ActionToken("reset");
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "weak" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "NewStrongPass2!" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "AnotherPass2!" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Clear();
        (await client.PostAsJsonAsync("/api/auth/login", new { email = "feature@example.com", password = "StrongPass1!" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new { email = "feature@example.com", password = "NewStrongPass2!" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RecoveryDoesNotExposeUnknownAccountsAndThrottlesMail()
    {
        await Register();
        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "feature@example.com" });
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@example.com" });
        (await known.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync());
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "feature@example.com" });
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ShopDbContext>().AccountActionTokens.CountAsync(x => x.Purpose == "reset")).Should().Be(1);
    }

    [Fact]
    public async Task ExpiredOrWrongPurposeTokensDoNotChangeAccount()
    {
        await Register();
        var token = await ActionToken("verify");
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "NewStrongPass2!" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        await db.AccountActionTokens.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await db.Users.SingleAsync(x => x.Email == "feature@example.com")).EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task FavoritesAreIdempotentAndPrivate()
    {
        (await client.GetAsync("/api/favorites")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        As();
        (await client.PutAsync("/api/favorites/1", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsync("/api/favorites/1", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Data(await client.GetAsync("/api/favorites"))).GetArrayLength().Should().Be(1);
        As("1");
        (await Data(await client.GetAsync("/api/favorites"))).GetArrayLength().Should().Be(0);
        await client.DeleteAsync("/api/favorites/1");
        As();
        (await Data(await client.GetAsync("/api/favorites"))).GetArrayLength().Should().Be(1);
        await client.DeleteAsync("/api/favorites/1");
        (await Data(await client.GetAsync("/api/favorites"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CompareEnforcesLimitOrderAndProductVisibility()
    {
        (await client.GetAsync("/api/products/compare?ids=1&ids=2&ids=3&ids=4")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/products/compare?ids=1&ids=1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var data = await Data(await client.GetAsync("/api/products/compare?ids=2&ids=1"));
        data[0].GetProperty("id").GetInt32().Should().Be(2);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Products.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        (await Data(await client.GetAsync("/api/products/compare?ids=1"))).GetArrayLength().Should().Be(0);
        As();
        (await client.PutAsync("/api/favorites/1", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GallerySupportsUploadCoverAndRemovalWithAuthorization()
    {
        As();
        (await client.GetAsync("/api/products/1/gallery")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        As("1", "Admin");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7FoAAAAASUVORK5CYII=")), "file", "image.png");
        var upload = await Data(await client.PostAsync("/api/products/1/gallery", form));
        var url = upload.GetProperty("imageUrl").GetString();
        using var second = new MultipartFormDataContent();
        second.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7FoAAAAASUVORK5CYII=")), "file", "second.png");
        var gallery = await Data(await client.PostAsync("/api/products/1/gallery", second));
        url = gallery.GetProperty("additionalImageUrls").EnumerateArray().Last().GetString();
        var cover = await Data(await client.PutAsJsonAsync("/api/products/1/gallery/cover", new { url }));
        cover.GetProperty("imageUrl").GetString().Should().Be(url);
        (await client.PutAsJsonAsync("/api/products/1/gallery/cover", new { url = "/not-owned.png" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var removal = new HttpRequestMessage(HttpMethod.Delete, "/api/products/1/gallery") { Content = JsonContent.Create(new { url }) };
        (await Data(await client.SendAsync(removal))).GetProperty("imageUrl").GetString().Should().NotBe(url);
    }

    [Fact]
    public async Task InvalidImageAndFullGalleryAreRejected()
    {
        As("1", "Admin");
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new StringContent("not a png"), "file", "fake.png");
        (await client.PostAsync("/api/products/1/gallery", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var product = (await db.Products.FindAsync(1))!;
        product.AdditionalImageUrls = Enumerable.Range(1, 7).Select(x => $"/image{x}.png").ToList();
        await db.SaveChangesAsync();
        (await client.PostAsync("/api/products/1/gallery", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LocalEmailDeliveryProducesAnEmlWithoutSmtp()
    {
        var environment = new Moq.Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Development");
        environment.SetupGet(x => x.ContentRootPath).Returns(factory.WebRoot);
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var delivery = new EmailDelivery(config, environment.Object);
        await delivery.SendAsync(new OutgoingEmail { Recipient = "mail@example.invalid", Subject = "Verify account", Body = "Local test link" }, default);
        var files = Directory.GetFiles(Path.Combine(factory.WebRoot, ".local-mail"), "*.eml");
        files.Should().HaveCount(1);
        (await File.ReadAllTextAsync(files[0])).Should().Contain("mail@example.invalid").And.Contain("Local test link");
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");
        var send = () => delivery.SendAsync(new OutgoingEmail { Recipient = "mail@example.invalid", Subject = "Test", Body = "Test" }, default);
        await send.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExpiredUnpaidOrderRestoresStockOnceAndRecordsSystemHistory()
    {
        As();
        await client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 2 });
        var order = await Data(await client.PostAsJsonAsync("/api/orders", new { fullName = "Test", shippingAddress = "123 Nguyen Trai", phone = "0900000000", paymentMethod = "VNPAY" }));
        var id = order.GetProperty("id").GetInt32();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        await db.Orders.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.PaymentExpiredAt, DateTime.UtcNow.AddMinutes(-1)));
        var job = scope.ServiceProvider.GetRequiredService<BadmintonShop.Api.Payments.PaymentExpiration>();
        await job.RunAsync(default);
        await job.RunAsync(default);
        (await db.Products.FindAsync(1))!.Stock.Should().Be(10);
        var history = await db.OrderHistories.Where(x => x.OrderId == id && x.ToValue == "Cancelled").SingleAsync();
        history.ActorId.Should().BeNull();
        history.ActorName.Should().Be("Hệ thống");
    }

    [Fact]
    public async Task OrderHistoryRecordsActorsAndProtectsOwnership()
    {
        As();
        await client.PostAsJsonAsync("/api/cart/items", new { productId = 1, quantity = 1 });
        var order = await Data(await client.PostAsJsonAsync("/api/orders", new { fullName = "Test", shippingAddress = "123 Nguyen Trai", phone = "0900000000", paymentMethod = "VNPAY" }));
        var id = order.GetProperty("id").GetInt32();
        var history = await Data(await client.GetAsync($"/api/orders/{id}/history"));
        history.GetArrayLength().Should().Be(1);
        history[0].GetProperty("actorId").GetInt32().Should().Be(2);
        As("1");
        (await client.GetAsync($"/api/orders/{id}/history")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        As("1", "Admin");
        var inventory = await Data(await client.GetAsync("/api/admin/inventory"));
        var stock = inventory.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == 1);
        stock.GetProperty("stock").GetInt32().Should().Be(9);
        stock.GetProperty("reserved").GetInt32().Should().Be(1);
        (await client.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "Confirmed" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "Shipping" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "Cancelled" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await Data(await client.GetAsync($"/api/admin/orders/{id}/history"));
        events.GetArrayLength().Should().Be(3);
        events[2].GetProperty("actorId").GetInt32().Should().Be(1);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Products.FindAsync(1))!.Stock.Should().Be(10);
    }
}
