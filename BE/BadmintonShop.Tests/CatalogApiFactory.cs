using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BadmintonShop.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace BadmintonShop.Tests;

internal sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    public Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor? SaveInterceptor { get; set; }
    public Dictionary<string, string?> ConfigurationOverrides { get; } = new();
    private const string SigningKey = "catalog-test-only-key-not-used-in-production";
    private readonly SqliteConnection _connection;
    private readonly bool concurrent;

    public string WebRoot { get; } = Path.Combine(AppContext.BaseDirectory, "test-webroots", Guid.NewGuid().ToString("N"));

    public CatalogApiFactory(bool concurrent = false)
    {
        Directory.CreateDirectory(WebRoot);
        this.concurrent = concurrent;
        _connection = new SqliteConnection(concurrent
            ? $"Data Source={Path.Combine(WebRoot, "test.db")};Foreign Keys=True;Pooling=False"
            : "Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseWebRoot(WebRoot);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey, ["Vnpay:ExpirationJobEnabled"] = "false" }.Concat(ConfigurationOverrides)));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ShopDbContext>();
            services.RemoveAll<DbContextOptions<ShopDbContext>>();
            services.AddDbContext<ShopDbContext>(options =>
            {
                if (SaveInterceptor is not null) options.AddInterceptors(SaveInterceptor);
                if (concurrent) options.UseSqlite(_connection.ConnectionString);
                else options.UseSqlite(_connection);
            });
        });
    }

    public static string Token(string role, string? signingKey = null, string userId = "1")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken("BadmintonShop", "BadmintonShop.Web",
            [new Claim("sub", userId), new Claim("role", role)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), credentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
        Directory.Delete(WebRoot, recursive: true);
    }
}
