using FluentValidation;

namespace BadmintonShop.Api.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150).WithMessage("Họ tên phải có từ 1 đến 150 ký tự.");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.Password).NotEmpty().Length(8, 128)
            .Matches("[a-z]").Matches("[A-Z]").Matches("[0-9]").Matches(@"[^a-zA-Z0-9\s]")
            .WithMessage("Mật khẩu cần 8–128 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128).WithMessage("Vui lòng nhập mật khẩu hợp lệ.");
    }
}
