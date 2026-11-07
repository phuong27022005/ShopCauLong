using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using FluentValidation;

namespace BadmintonShop.Api.Orders;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ShippingAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+?[0-9]{9,15}$").WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.PaymentMethod).Must(x => x is "COD" or "VNPAY").WithMessage("Phương thức phải là COD hoặc VNPAY.");
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.FullName).MaximumLength(150);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
public sealed class OrderQueryValidator : AbstractValidator<OrderQuery>
{
    public OrderQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Status).Must(x => x is null || OrderRules.Statuses.Contains(x));
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x).Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage("Ngày bắt đầu không được sau ngày kết thúc.");
    }
}
public sealed class OrderStatusValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public OrderStatusValidator() => RuleFor(x => x.Status).Must(OrderRules.Statuses.Contains);
}

