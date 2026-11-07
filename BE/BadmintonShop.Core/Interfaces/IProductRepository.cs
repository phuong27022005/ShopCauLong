using BadmintonShop.Core.Entities;

namespace BadmintonShop.Core.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
