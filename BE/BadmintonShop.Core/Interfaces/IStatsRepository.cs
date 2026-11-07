using BadmintonShop.Core.DTOs;

namespace BadmintonShop.Core.Interfaces;

public interface IStatsRepository
{
    Task<StatsOverview> OverviewAsync(StatsQuery query, CancellationToken ct);
    Task<IReadOnlyList<RevenuePoint>> RevenueAsync(StatsQuery query, CancellationToken ct);
    Task<IReadOnlyList<TopProduct>> TopProductsAsync(int limit, CancellationToken ct);
}

