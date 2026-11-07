namespace BadmintonShop.Core.DTOs;

public sealed class StatsQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string GroupBy { get; init; } = "day";
}
public sealed class TopProductsQuery
{
    public int Limit { get; init; } = 10;
}
public sealed record OrderStatusCount(string Status, int Count);
public sealed record StatsOverview(decimal TotalRevenue, IReadOnlyList<OrderStatusCount> OrdersByStatus,
    int NewCustomers, int LowStockProducts);
public sealed record RevenuePoint(DateOnly Period, decimal Revenue);
public sealed record TopProduct(int ProductId, string Name, long QuantitySold);

