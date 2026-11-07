using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class ProductRepository(ShopDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
}
