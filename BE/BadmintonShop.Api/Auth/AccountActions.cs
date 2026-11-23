using System.Security.Cryptography;
using System.Text;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Api.Auth;

public sealed record EmailRequest(string Email);
public sealed record ConfirmEmailRequest(string Token);
public sealed record ResetPasswordRequest(string Token, string Password);
public sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator() => RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
}
public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator() => RuleFor(x => x.Token).NotEmpty().Length(96).Matches("^[A-Fa-f0-9]+$");
}
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Length(96).Matches("^[A-Fa-f0-9]+$");
        RuleFor(x => x.Password).NotEmpty().Length(8,128).Matches("[a-z]").Matches("[A-Z]")
            .Matches("[0-9]").Matches(@"[^a-zA-Z0-9\s]");
    }
}

public sealed class AccountActions(ShopDbContext db, IConfiguration configuration, TimeProvider clock)
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Token and outgoing message are persisted in the same SaveChanges transaction.
    public void Queue(User user, string purpose)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        var now = clock.GetUtcNow().UtcDateTime;
        var reset = purpose == "reset";
        db.AccountActionTokens.Add(new AccountActionToken { User = user, Purpose = purpose,
            TokenHash = Hash(token), ExpiresAt = now.AddMinutes(reset ? 30 : 1440) });
        var origin = configuration["Email:FrontendUrl"] ?? "http://localhost:5173";
        var link = origin.TrimEnd('/') + (reset ? "/reset-password" : "/verify-email") + "#token=" + token;
        db.OutgoingEmails.Add(new OutgoingEmail { Recipient = user.Email,
            Subject = reset ? "Đặt lại mật khẩu Badminton Shop" : "Xác minh email Badminton Shop",
            Body = $"Xin chào {user.FullName},\n\n{(reset ? "Đặt lại mật khẩu" : "Xác minh email")} tại:\n{link}\n\nLiên kết dùng một lần, hết hạn sau {(reset ? "30 phút" : "24 giờ")}. Nếu không yêu cầu, bạn có thể bỏ qua email này.",
            CreatedAt = now, NextAttemptAt = now });
    }

    public async Task RequestAsync(string email, string purpose, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null || purpose == "verify" && user.EmailConfirmed) return;
        var now = clock.GetUtcNow().UtcDateTime;
        // Bound mail volume per account, independently of the per-IP HTTP limit.
        var subject = purpose == "reset" ? "Đặt lại mật khẩu Badminton Shop" : "Xác minh email Badminton Shop";
        if (await db.OutgoingEmails.AnyAsync(x => x.Recipient == email && x.Subject == subject && x.CreatedAt > now.AddMinutes(-1), ct)) return;
        Queue(user, purpose);
        await db.SaveChangesAsync(ct);
    }

    public async Task ConsumeAsync(string token, string purpose, string? password, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var hash = Hash(token);
        var now = clock.GetUtcNow().UtcDateTime;
        var item = await db.AccountActionTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash && x.Purpose == purpose && x.ExpiresAt > now, ct);
        if (item is null) throw CatalogException.BadRequest("Liên kết không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.");
        var consumed = await db.AccountActionTokens.Where(x => x.Id == item.Id && x.ExpiresAt > now).ExecuteDeleteAsync(ct);
        if (consumed != 1) throw CatalogException.BadRequest("Liên kết đã được sử dụng.");
        var user = await db.Users.SingleAsync(x => x.Id == item.UserId, ct);
        if (purpose == "reset")
        {
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password!);
            user.TokenVersion++;
            await db.RefreshSessions.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
        }
        else user.EmailConfirmed = true;
        await db.AccountActionTokens.Where(x => x.UserId == user.Id && x.Purpose == purpose).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
