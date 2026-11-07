using System.Text;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using BadmintonShop.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<ShopDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<IStatsRepository, StatsRepository>();
builder.Services.AddScoped<BadmintonShop.Api.Stats.StatsService>();
builder.Services.AddScoped<IEngagementRepository, EngagementRepository>();
builder.Services.AddScoped<BadmintonShop.Api.Engagement.CouponService>();
builder.Services.AddScoped<BadmintonShop.Api.Engagement.ReviewService>();
builder.Services.Configure<BadmintonShop.Api.Payments.VnpayOptions>(builder.Configuration.GetSection("Vnpay"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<BadmintonShop.Api.Payments.VnpayService>();
builder.Services.AddScoped<BadmintonShop.Api.Payments.PaymentExpiration>();
builder.Services.AddHostedService<BadmintonShop.Api.Payments.PaymentExpirationWorker>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<BadmintonShop.Api.Orders.OrderService>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<BadmintonShop.Api.Cart.CartService>();
builder.Services.AddSingleton<ProductImageStore>();
builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"],
    typeof(CatalogMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<SaveProductValidator>();
builder.Services.AddControllers(options => options.Filters.Add<ValidateCatalogRequestFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "Giá trị không hợp lệ." : error.ErrorMessage)
                    .ToArray());
            return new BadRequestObjectResult(ApiResponse.Error("Dữ liệu yêu cầu không hợp lệ.", errors));
        };
    });
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"];
    if (!string.IsNullOrEmpty(signingKey) && Encoding.UTF8.GetByteCount(signingKey) < 32)
        throw new InvalidOperationException("Jwt:SigningKey phải có ít nhất 32 byte.");

    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "BadmintonShop",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "BadmintonShop.Web",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = string.IsNullOrEmpty(signingKey) ? null : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly",
    policy => policy.RequireAuthenticatedUser().RequireRole("Admin")));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStatusCodePages(async status =>
{
    var response = status.HttpContext.Response;
    var message = response.StatusCode switch
    {
        401 => "Vui lòng đăng nhập.",
        403 => "Bạn không có quyền thực hiện thao tác này.",
        404 => "Không tìm thấy tài nguyên.",
        _ => "Không thể xử lý yêu cầu."
    };
    await response.WriteAsJsonAsync(ApiResponse.Error(message), status.HttpContext.RequestAborted);
});
app.UseMiddleware<CatalogErrorMiddleware>();

var uploadDirectory = Path.Combine(app.Environment.WebRootPath ??
    Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(uploadDirectory);
var uploadFiles = new PhysicalFileProvider(uploadDirectory);
app.Lifetime.ApplicationStopped.Register(uploadFiles.Dispose);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = uploadFiles,
    RequestPath = "/uploads",
    OnPrepareResponse = context => context.Context.Response.Headers.XContentTypeOptions = "nosniff"
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
