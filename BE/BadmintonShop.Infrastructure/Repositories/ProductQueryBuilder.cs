using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public static class ProductQueryBuilder
{
    public static IQueryable<Product> Filter(IQueryable<Product> source, ProductPageQuery q, bool includeHidden = false)
    {
        if (!includeHidden) source = source.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = SearchText.Normalize(q.Search.Trim());
            source = source.Where(x => x.SearchName.Contains(search));
        }
        if (q.BrandId.Length > 0) source = source.Where(x => q.BrandId.Contains(x.BrandId));
        if (q.CategoryId.HasValue) source = source.Where(x => x.CategoryId == q.CategoryId);
        if (q.MinPrice.HasValue) source = source.Where(x => x.Price >= q.MinPrice);
        if (q.MaxPrice.HasValue) source = source.Where(x => x.Price <= q.MaxPrice);
        if (q.Weight.Length > 0) source = source.Where(x => q.Weight.Contains(x.Weight));
        if (!string.IsNullOrWhiteSpace(q.Stiffness)) source = source.Where(x => x.Stiffness == q.Stiffness);
        if (!string.IsNullOrWhiteSpace(q.BalancePoint)) source = source.Where(x => x.BalancePoint == q.BalancePoint);
        if (q.InStock) source = source.Where(x => x.Stock > 0);
        return source;
    }

    public static IOrderedQueryable<Product> Sort(IQueryable<Product> source, ProductPageQuery q, bool sqlite)
    {
        var descending = q.SortDir == "desc";
        // SQLite không hỗ trợ ORDER BY decimal, chuyển kiểu tại DB cho bộ test.
        return q.SortBy switch
        {
            "price" when sqlite => descending
                ? source.OrderByDescending(x => (double)x.Price).ThenBy(x => x.Id)
                : source.OrderBy(x => (double)x.Price).ThenBy(x => x.Id),
            "price" => descending ? source.OrderByDescending(x => x.Price).ThenBy(x => x.Id) : source.OrderBy(x => x.Price).ThenBy(x => x.Id),
            "name" => descending ? source.OrderByDescending(x => x.Name).ThenBy(x => x.Id) : source.OrderBy(x => x.Name).ThenBy(x => x.Id),
            "createdAt" => descending ? source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id) : source.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ when q.NewestFirst => source.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            _ => source.OrderBy(x => x.Id)
        };
    }
}
