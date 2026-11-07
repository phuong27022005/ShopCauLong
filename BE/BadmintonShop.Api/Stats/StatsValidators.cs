using BadmintonShop.Core.DTOs;
using FluentValidation;

namespace BadmintonShop.Api.Stats;

public sealed class StatsQueryValidator : AbstractValidator<StatsQuery>
{
    public StatsQueryValidator()
    {
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("from phải nhỏ hơn hoặc bằng to.");
        RuleFor(x => x.GroupBy).Must(x => x is "day" or "month")
            .WithMessage("groupBy phải là day hoặc month.");
    }
}
public sealed class TopProductsQueryValidator : AbstractValidator<TopProductsQuery>
{
    public TopProductsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 100).WithMessage("limit phải từ 1 đến 100.");
    }
}

