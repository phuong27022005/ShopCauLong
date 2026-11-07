using BadmintonShop.Core.DTOs;
using FluentValidation;

namespace BadmintonShop.Api.Engagement;

public sealed class ReviewValidator : AbstractValidator<ReviewRequest>
{
    public ReviewValidator()
    {
        RuleFor(x=>x.Rating).InclusiveBetween(1,5).WithMessage("Rating phải từ 1 đến 5.");
        RuleFor(x=>x.Comment).NotNull().MaximumLength(2000);
    }
}
public sealed class CouponValidator : AbstractValidator<CouponRequest>
{
    public CouponValidator()
    {
        RuleFor(x=>x.Code).NotEmpty().MaximumLength(50).Matches("^[a-zA-Z0-9_-]+$");
        RuleFor(x=>x.Type).Must(x=>x is "Percent" or "Fixed");
        RuleFor(x=>x.Value).GreaterThan(0).PrecisionScale(18,2,true);
        RuleFor(x=>x.Value).LessThanOrEqualTo(100).When(x=>x.Type=="Percent");
        RuleFor(x=>x.MinOrderAmount).GreaterThanOrEqualTo(0).PrecisionScale(18,2,true);
        RuleFor(x=>x.MaxDiscount).GreaterThanOrEqualTo(0).PrecisionScale(18,2,true);
        RuleFor(x=>x.UsageLimit).GreaterThan(0);
        RuleFor(x=>x.EndAt).GreaterThan(x=>x.StartAt);
    }
}
public sealed class ValidateCouponValidator : AbstractValidator<ValidateCouponRequest>
{
    public ValidateCouponValidator()
    {
        RuleFor(x=>x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x=>x.Subtotal).GreaterThanOrEqualTo(0).PrecisionScale(18,2,true);
    }
}
