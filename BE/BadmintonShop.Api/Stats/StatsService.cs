using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Stats;

public sealed class StatsService(IStatsRepository repository)
{
    public Task<StatsOverview> OverviewAsync(StatsQuery query, CancellationToken ct) =>
        repository.OverviewAsync(query, ct);
    public Task<IReadOnlyList<RevenuePoint>> RevenueAsync(StatsQuery query, CancellationToken ct) =>
        repository.RevenueAsync(query, ct);
    public Task<IReadOnlyList<TopProduct>> TopProductsAsync(int limit, CancellationToken ct) =>
        repository.TopProductsAsync(limit, ct);
}

