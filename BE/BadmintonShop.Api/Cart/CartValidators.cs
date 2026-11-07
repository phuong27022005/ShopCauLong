using BadmintonShop.Core.DTOs;
using FluentValidation;

namespace BadmintonShop.Api.Cart;

public sealed class AddCartItemValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 10).WithMessage("Số lượng phải từ 1 đến 10.");
    }
}
public sealed class UpdateCartItemValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemValidator() =>
        RuleFor(x => x.Quantity).InclusiveBetween(1, 10).WithMessage("Số lượng phải từ 1 đến 10.");
}
public sealed class MergeCartValidator : AbstractValidator<MergeCartRequest>
{
    public MergeCartValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(x => x is null || x.Count <= 100)
            .WithMessage("Giỏ gộp tối đa 100 dòng.");
        RuleForEach(x => x.Items).NotNull().SetValidator(new AddCartItemValidator());
    }
}

