using System.Linq.Expressions;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;

namespace BadmintonShop.Core.Interfaces;

public interface ICatalogRepository
{
    Task<PageDto<Product>> ListProductsAsync(ProductPageQuery options, CancellationToken ct, bool includeHidden = false);
    Task<Product?> FindPublicProductAsync(string slug, CancellationToken ct);
    Task<List<T>> ListAsync<T>(CancellationToken ct) where T : class;
    Task<T?> FindAsync<T>(int id, CancellationToken ct) where T : class;
    Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> filter, CancellationToken ct) where T : class;
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task SaveAsync(CancellationToken ct);
}

public sealed class CatalogSlugConflictException(Exception cause) : Exception("Slug đã được sử dụng.", cause);
public sealed class CatalogReferenceConflictException(Exception cause) : Exception("Dữ liệu đang được tham chiếu.", cause);
