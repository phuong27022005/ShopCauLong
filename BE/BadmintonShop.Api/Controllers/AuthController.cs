using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BadmintonShop.Api.Auth;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(ShopDbContext db, IConfiguration configuration, TimeProvider clock, AccountActions actions) : ControllerBase
{
    private const string CookieName = "badminton.refresh";
    private readonly PasswordHasher<User> hasher = new();
    private static readonly User DummyUser = new();
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(DummyUser, "dummy-password");
    private static object Profile(User user) => new { user.Id, user.FullName, user.Email, user.Role, user.EmailConfirmed };
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private CookieOptions Cookie(DateTime? expires = null) => new()
    {
        HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict,
        Path = "/api/auth", Expires = expires
    };
    private string AccessToken(User user)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Cần cấu hình Jwt:SigningKey ít nhất 32 byte.");
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            configuration["Jwt:Issuer"] ?? "BadmintonShop", configuration["Jwt:Audience"] ?? "BadmintonShop.Web",
            [new Claim("sub", user.Id.ToString()), new Claim("role", user.Role), new Claim("ver", user.TokenVersion.ToString()), new Claim("jti", Guid.NewGuid().ToString())],
            Now, Now.AddMinutes(15), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct))
            throw CatalogException.Conflict("Email đã được sử dụng.");
        var user = new User { FullName = request.FullName.Trim(), Email = email, Role = "Customer", CreatedAt = Now };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        actions.Queue(user, "verify");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.Users.AnyAsync(x => x.Email == email, ct))
                throw CatalogException.Conflict("Email đã được sử dụng.");
            throw;
        }
        return StatusCode(201, ApiResponse.Ok(message: "Đăng ký thành công. Vui lòng đăng nhập."));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest input, CancellationToken ct)
    {
        await actions.RequestAsync(input.Email, "reset", ct);
        return Ok(ApiResponse.Ok(message: "Nếu email đã đăng ký, bạn sẽ nhận được hướng dẫn. Vui lòng kiểm tra hộp thư."));
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(EmailRequest input, CancellationToken ct)
    {
        await actions.RequestAsync(input.Email, "verify", ct);
        return Ok(ApiResponse.Ok(message: "Nếu tài khoản cần xác minh, liên kết sẽ được gửi qua email. Vui lòng chờ ít nhất một phút giữa các lần gửi."));
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(ConfirmEmailRequest input, CancellationToken ct)
    {
        await actions.ConsumeAsync(input.Token, "verify", null, ct);
        return Ok(ApiResponse.Ok(message: "Đã xác minh email."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest input, CancellationToken ct)
    {
        await actions.ConsumeAsync(input.Token, "reset", input.Password, ct);
        Response.Cookies.Delete(CookieName, Cookie());
        return Ok(ApiResponse.Ok(message: "Đã đổi mật khẩu. Vui lòng đăng nhập lại trên các thiết bị."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        PasswordVerificationResult result;
        try { result = hasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? DummyHash, request.Password); }
        catch (FormatException) { result = PasswordVerificationResult.Failed; }
        if (user is null || result == PasswordVerificationResult.Failed)
            return Unauthorized(ApiResponse.Error("Email hoặc mật khẩu không đúng."));
        var accessToken = AccessToken(user);
        if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, request.Password);
        if (Request.Cookies.TryGetValue(CookieName, out var oldToken))
            await db.RefreshSessions.Where(x => x.TokenHash == Hash(oldToken)).ExecuteDeleteAsync(ct);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var expires = Now.AddDays(7);
        db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, TokenHash = Hash(token), ExpiresAt = expires });
        await db.SaveChangesAsync(ct);
        Response.Cookies.Append(CookieName, token, Cookie(expires));
        return Ok(ApiResponse.Ok(new { user = Profile(user), accessToken }));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var id)) return Unauthorized(ApiResponse.Error("Phiên không hợp lệ."));
        var user = await db.Users.FindAsync([id], ct);
        return user is null ? Unauthorized(ApiResponse.Error("Tài khoản không tồn tại.")) : Ok(ApiResponse.Ok(Profile(user)));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var token)) return Unauthorized(ApiResponse.Error("Vui lòng đăng nhập."));
        var hash = Hash(token);
        var session = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > Now, ct);
        var user = session is null ? null : await db.Users.FindAsync([session.UserId], ct);
        if (user is null) return Unauthorized(ApiResponse.Error("Phiên đã hết hạn. Vui lòng đăng nhập lại."));
        var accessToken = AccessToken(user);
        var replacement = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        // Atomic compare-and-swap ensures a refresh token can only be consumed once.
        var changed = await db.RefreshSessions.Where(x => x.Id == session!.Id && x.TokenHash == hash && x.ExpiresAt > Now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.TokenHash, Hash(replacement)), ct);
        if (changed != 1) return Unauthorized(ApiResponse.Error("Phiên không hợp lệ."));
        Response.Cookies.Append(CookieName, replacement, Cookie(session!.ExpiresAt));
        return Ok(ApiResponse.Ok(new { accessToken }));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(CookieName, out var token))
            await db.RefreshSessions.Where(x => x.TokenHash == Hash(token)).ExecuteDeleteAsync(ct);
        Response.Cookies.Delete(CookieName, Cookie());
        return Ok(ApiResponse.Ok(message: "Đã đăng xuất."));
    }
}
