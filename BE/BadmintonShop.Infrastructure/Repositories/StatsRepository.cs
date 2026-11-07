using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class StatsRepository(ShopDbContext db) : IStatsRepository
{
    private static IQueryable<Order> Eligible(IQueryable<Order> orders) =>
        orders.Where(x => x.Status != "Cancelled" && (x.Status == "Completed" || x.PaymentStatus == "Paid"));

    private IQueryable<Order> Orders(StatsQuery query)
    {
        var orders = db.Orders.AsNoTracking();
        if (query.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(x => x.CreatedAt >= start);
        }
        if (query.To is { } to && to != DateOnly.MaxValue)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(x => x.CreatedAt < end);
        }
        return orders;
    }

    public async Task<StatsOverview> OverviewAsync(StatsQuery query, CancellationToken ct)
    {
        var orders = Orders(query);
        var eligible = Eligible(orders);
        // SQLite không hỗ trợ SUM(decimal); cộng đơn vị 0,01 tại DB để giữ phần lẻ.
        var revenue = db.Database.IsSqlServer()
            ? await eligible.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m
            : (await eligible.SumAsync(x => (long?)Math.Round((double)x.TotalAmount * 100d), ct) ?? 0L) / 100m;
        var statuses = await orders.GroupBy(x => x.Status)
            .OrderBy(g => g.Key).Select(g => new OrderStatusCount(g.Key, g.Count())).ToListAsync(ct);
        var customers = db.Users.AsNoTracking().Where(x => x.Role == "Customer");
        if (query.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            customers = customers.Where(x => x.CreatedAt >= start);
        }
        if (query.To is { } to && to != DateOnly.MaxValue)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            customers = customers.Where(x => x.CreatedAt < end);
        }
        var newCustomers = await customers.CountAsync(ct);
        var lowStock = await db.Products.CountAsync(x => x.IsActive && x.Stock <= 5, ct);
        return new(revenue, statuses, newCustomers, lowStock);
    }

    public async Task<IReadOnlyList<RevenuePoint>> RevenueAsync(StatsQuery query, CancellationToken ct)
    {
        var monthly = query.GroupBy == "month";
        var groups = Eligible(Orders(query)).GroupBy(x => new
        {
            x.CreatedAt.Year, x.CreatedAt.Month, Day = monthly ? 1 : x.CreatedAt.Day
        });
        if (db.Database.IsSqlServer())
        {
            var rows = await groups.Select(g => new
            {
                g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(x => x.TotalAmount)
            }).OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToListAsync(ct);
            return rows.Select(x => new RevenuePoint(new DateOnly(x.Year, x.Month, x.Day), x.Amount)).ToList();
        }
        var cents = await groups.Select(g => new
        {
            g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(x => (long)Math.Round((double)x.TotalAmount * 100d))
        }).OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToListAsync(ct);
        // Chỉ định dạng kết quả sau khi DB đã lọc, nhóm và cộng.
        return cents.Select(x => new RevenuePoint(new DateOnly(x.Year, x.Month, x.Day), x.Amount / 100m)).ToList();
    }

    public async Task<IReadOnlyList<TopProduct>> TopProductsAsync(int limit, CancellationToken ct) =>
        await (from item in db.OrderItems.AsNoTracking()
               join order in Eligible(db.Orders.AsNoTracking()) on item.OrderId equals order.Id
               join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
               group item by new { product.Id, product.Name } into g
               select new { ProductId = g.Key.Id, g.Key.Name, QuantitySold = g.Sum(x => (long)x.Quantity) })
            .OrderByDescending(x => x.QuantitySold).ThenBy(x => x.ProductId).Take(limit)
            .Select(x => new TopProduct(x.ProductId, x.Name, x.QuantitySold)).ToListAsync(ct);
}

