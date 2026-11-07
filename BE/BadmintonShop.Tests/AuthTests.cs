using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BadmintonShop.Tests;

public sealed class AuthTests : IAsyncLifetime
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
    private Task<HttpResponseMessage> Register(string email = "new@example.com", string password = "StrongPass1!") =>
        client.PostAsJsonAsync("/api/auth/register", new { fullName = " Nguyễn An ", email, password, role = "Admin" });
    private Task<HttpResponseMessage> Login(string password = "StrongPass1!") =>
        client.PostAsJsonAsync("/api/auth/login", new { email = "NEW@example.com", password });
    private static async Task<JsonElement> Data(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    private static string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    [Fact]
    public async Task RegisterLoginProfileRefreshAndLogout()
    {
        (await Register()).StatusCode.Should().Be(HttpStatusCode.Created);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Users.SingleAsync(x => x.Email == "new@example.com");
            user.PasswordHash.Should().NotBe("StrongPass1!");
            user.FullName.Should().Be("Nguyễn An");
            user.Role.Should().Be("Customer");
        }
        var login = await Login();
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await Data(login);
        data.GetProperty("user").TryGetProperty("passwordHash", out _).Should().BeFalse();
        var originalCookie = Cookie(login);
        login.Headers.GetValues("Set-Cookie").Single().Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/admin/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Add("Cookie", originalCookie);
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        Cookie(refresh).Should().NotBe(originalCookie);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", Cookie(refresh));
        (await client.PostAsync("/api/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DuplicateEmailAndWrongPasswordAreRejected()
    {
        (await Register()).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Register("NEW@example.com")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Login("WrongPass1!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("bad-email", "StrongPass1!")]
    [InlineData("new@example.com", "weak")]
    [InlineData("new@example.com", "NoDigits!")]
    public async Task InvalidRegistrationIsRejected(string email, string password)
    {
        (await Register(email, password)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExpiredRefreshIsRejected()
    {
        await Register();
        var login = await Login();
        client.DefaultRequestHeaders.Add("Cookie", Cookie(login));
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().RefreshSessions.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
