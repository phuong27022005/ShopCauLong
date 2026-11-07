using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Repositories;
using FluentAssertions;

namespace BadmintonShop.Tests;

public sealed class ProductQueryBuilderTests
{
    [Fact]
    public void EveryConditionFiltersBeforeMaterialization()
    {
        var match = new Product
        {
            Id = 1, IsActive = true, Name = "Vợt Đỏ", SearchName = SearchText.Normalize("Vợt Đỏ"),
            BrandId = 1, CategoryId = 2, Price = 100, Weight = "4U",
            Stiffness = "Cứng", BalancePoint = "Nặng đầu", Stock = 1
        };
        var other = new Product { Id = 2, IsActive = true, Price = 200 };
        var source = new[] { match, other, new Product { Id = 3, IsActive = false } }.AsQueryable();
        ProductPageQuery[] filters =
        [
            new() { Search = "VOT DO" }, new() { BrandId = [1] }, new() { CategoryId = 2 },
            new() { MinPrice = 90, MaxPrice = 110 }, new() { Weight = ["4U"] },
            new() { Stiffness = "Cứng" }, new() { BalancePoint = "Nặng đầu" }, new() { InStock = true }
        ];
        foreach (var filter in filters)
            ProductQueryBuilder.Filter(source, filter).Select(x => x.Id).Should().Equal(1);
    }
}
