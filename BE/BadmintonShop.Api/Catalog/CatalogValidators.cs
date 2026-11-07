using BadmintonShop.Core.DTOs;
using FluentValidation;

namespace BadmintonShop.Api.Catalog;

public sealed class ProductPageValidator : AbstractValidator<ProductPageQuery>
{
    public ProductPageValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200);
        RuleForEach(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MinPrice <= x.MaxPrice)
            .WithMessage("minPrice không được lớn hơn maxPrice.");
        RuleForEach(x => x.Weight).Must(x => x is "2U" or "3U" or "4U" or "5U" or "F")
            .WithMessage("weight không hợp lệ.");
        RuleFor(x => x.SortBy).Must(x => x is null or "price" or "name" or "createdAt").WithMessage("sortBy phải là price, name hoặc createdAt.");
        RuleFor(x => x.SortDir).Must(x => x is "asc" or "desc").WithMessage("sortDir phải là asc hoặc desc.");
        RuleFor(x => x.Stiffness).MaximumLength(50);
        RuleFor(x => x.BalancePoint).MaximumLength(50);
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("pageSize phải từ 1 đến 50.");
    }
}

public sealed class SaveProductValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên sản phẩm là bắt buộc.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Giá phải lớn hơn 0.").PrecisionScale(18, 2, true);
        RuleFor(x => x.DiscountPrice).GreaterThanOrEqualTo(0)
            .LessThan(x => x.Price).WithMessage("Giá giảm phải thấp hơn giá gốc.")
            .PrecisionScale(18, 2, true);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0).WithMessage("Tồn kho không được âm.");
        RuleFor(x => x.Weight).Must(weight => weight is "2U" or "3U" or "4U" or "5U" or "F")
            .WithMessage("Trọng lượng phải là 2U, 3U, 4U, 5U hoặc F.");
        RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Stiffness).MaximumLength(50);
        RuleFor(x => x.BalancePoint).MaximumLength(50);
        RuleFor(x => x.ImageUrl).MaximumLength(2048);
    }
}

public sealed class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên danh mục là bắt buộc.").MaximumLength(100);
}

public sealed class SaveBrandValidator : AbstractValidator<SaveBrandRequest>
{
    public SaveBrandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên thương hiệu là bắt buộc.").MaximumLength(100);
        RuleFor(x => x.Logo).MaximumLength(2048);
    }
}
