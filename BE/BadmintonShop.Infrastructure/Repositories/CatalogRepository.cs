using System.Linq.Expressions;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class CatalogRepository(ShopDbContext db) : ICatalogRepository
{
    public async Task<PageDto<Product>> ListProductsAsync(ProductPageQuery options, CancellationToken ct, bool includeHidden = false)
    {
        var page = options.Page;
        var pageSize = options.PageSize;
        var query = ProductQueryBuilder.Filter(db.Products.AsNoTracking(), options, includeHidden);
        var count = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var ordered = ProductQueryBuilder.Sort(query, options, db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite");
        // Kiểm tra trước khi ép về int để page rất lớn vẫn trả trang rỗng.
        var items = offset >= count
            ? []
            : await ordered.Include(product => product.Brand).Include(product => product.Category).Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return new PageDto<Product>(items, page, pageSize, count);
    }

    public Task<Product?> FindPublicProductAsync(string slug, CancellationToken ct) =>
        db.Products.AsNoTracking().Include(x => x.Brand).Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct);

    public Task<List<T>> ListAsync<T>(CancellationToken ct) where T : class =>
        db.Set<T>().AsNoTracking().OrderBy(x => EF.Property<int>(x, "Id")).ToListAsync(ct);

    public async Task<T?> FindAsync<T>(int id, CancellationToken ct) where T : class =>
        await db.Set<T>().FindAsync([id], ct);

    public Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> filter, CancellationToken ct) where T : class =>
        db.Set<T>().AnyAsync(filter, ct);

    public void Add<T>(T entity) where T : class => db.Set<T>().Add(entity);
    public void Remove<T>(T entity) where T : class => db.Set<T>().Remove(entity);

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new CatalogSlugConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            throw new CatalogReferenceConflictException(ex);
        }
    }
}
