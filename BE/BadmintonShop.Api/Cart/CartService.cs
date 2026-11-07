using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Cart;

public sealed class CartService(ICartRepository repository)
{
    public Task<CartDto> GetAsync(int userId, CancellationToken ct) =>
        repository.WithCartAsync(userId, (_, items) => SnapshotAsync(items, ct), ct);

    public Task<CartDto> AddAsync(int userId, AddCartItemRequest request, CancellationToken ct) =>
        MergeAsync(userId, new MergeCartRequest([request]), ct);

    public Task<CartDto> MergeAsync(int userId, MergeCartRequest request, CancellationToken ct) =>
        repository.WithCartAsync(userId, async (cart, items) =>
        {
            var products = await repository.ProductsAsync(request.Items.Select(x => x.ProductId), ct);
            foreach (var group in request.Items.GroupBy(x => x.ProductId))
            {
                var product = products.SingleOrDefault(x => x.Id == group.Key)
                    ?? throw CatalogException.NotFound("Sản phẩm");
                var item = items.SingleOrDefault(x => x.ProductId == group.Key);
                var quantity = group.Sum(x => (long)x.Quantity) + (item?.Quantity ?? 0);
                ValidateQuantity(product, quantity);
                if (item is null)
                {
                    item = new CartItem { CartId = cart.Id, ProductId = group.Key };
                    repository.Add(item);
                    items.Add(item);
                }
                item.Quantity = (int)quantity;
            }
            return await SnapshotAsync(items, ct);
        }, ct);

    public Task<CartDto> UpdateAsync(int userId, int productId, int quantity, CancellationToken ct) =>
        repository.WithCartAsync(userId, async (_, items) =>
        {
            var item = items.SingleOrDefault(x => x.ProductId == productId)
                ?? throw CatalogException.NotFound("Dòng giỏ hàng");
            var product = (await repository.ProductsAsync([productId], ct)).Single();
            ValidateQuantity(product, quantity);
            item.Quantity = quantity;
            return await SnapshotAsync(items, ct);
        }, ct);

    public Task<CartDto> DeleteAsync(int userId, int? productId, CancellationToken ct) =>
        repository.WithCartAsync(userId, async (_, items) =>
        {
            if (productId.HasValue && items.All(x => x.ProductId != productId))
                throw CatalogException.NotFound("Dòng giỏ hàng");
            foreach (var item in items.Where(x => !productId.HasValue || x.ProductId == productId).ToList())
            {
                repository.Remove(item);
                items.Remove(item);
            }
            return await SnapshotAsync(items, ct);
        }, ct);

    public static void ValidateQuantity(Product product, long quantity)
    {
        if (!product.IsActive || product.Stock <= 0)
            throw CatalogException.BadRequest("Sản phẩm hiện không có sẵn.");
        if (quantity is < 1 or > 10)
            throw CatalogException.BadRequest("Tổng số lượng mỗi sản phẩm phải từ 1 đến 10.");
        if (quantity > product.Stock)
            throw CatalogException.BadRequest("Số lượng vượt quá tồn kho.");
    }

    public static CartDto Calculate(IReadOnlyList<CartItem> items, IReadOnlyList<Product> products)
    {
        var lines = items.Select(item =>
        {
            var product = products.Single(x => x.Id == item.ProductId);
            var price = product.DiscountPrice ?? product.Price;
            return new CartLineDto(item.ProductId, item.Quantity,
                new CartProductDto(product.Id, product.Name, product.Slug, product.ImageUrl,
                    product.Price, product.DiscountPrice, product.Stock, product.IsActive),
                price, price * item.Quantity, product.IsActive && product.Stock >= item.Quantity);
        }).ToList();
        // Tổng tham khảo gồm các dòng hiện có; isAvailable báo dòng cần điều chỉnh.
        return new CartDto(lines, lines.Sum(x => x.LineTotal));
    }

    private async Task<CartDto> SnapshotAsync(List<CartItem> items, CancellationToken ct) =>
        Calculate(items, await repository.ProductsAsync(items.Select(x => x.ProductId), ct));
}

