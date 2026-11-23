using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Orders;

public sealed class OrderService(IOrderRepository repository, BadmintonShop.Api.Engagement.CouponService coupons)
{
    public Task<OrderDto> CreateAsync(int userId, CreateOrderRequest input, CancellationToken ct) =>
        repository.TransactionAsync(async () =>
        {
            var cart = await repository.CartItemsAsync(userId, ct);
            if (cart.Count == 0) throw CatalogException.BadRequest("Giỏ hàng đang trống.");
            var products = await repository.ProductsAsync(cart.Select(x => x.ProductId), ct);
            var lines = new List<OrderItem>();
            foreach (var item in cart)
            {
                var product = products.SingleOrDefault(x => x.Id == item.ProductId);
                if (product is null || !product.IsActive || item.Quantity is < 1 or > 10 || product.Stock < item.Quantity)
                    throw CatalogException.BadRequest($"Sản phẩm {product?.Name ?? item.ProductId.ToString()} không còn bán hoặc không đủ tồn kho.");
                lines.Add(new OrderItem { ProductId = product.Id, ProductName = product.Name,
                    Quantity = item.Quantity, UnitPrice = product.DiscountPrice ?? product.Price });
            }
            // Chỉ ghi sau khi đã kiểm tra toàn bộ giỏ.
            foreach (var item in cart) products.Single(x => x.Id == item.ProductId).Stock -= item.Quantity;
            var order = new Order
            {
                UserId = userId, Status = "Pending", CreatedAt = DateTime.UtcNow,
                FullName = input.FullName?.Trim(), ShippingAddress = input.ShippingAddress.Trim(), Phone = input.Phone.Trim(),
                PaymentMethod = input.PaymentMethod, Note = input.Note?.Trim(), TotalAmount = OrderRules.Total(lines)
            };
            if (order.PaymentMethod == "VNPAY") order.PaymentExpiredAt = order.CreatedAt.AddMinutes(15);
            await coupons.ApplyAsync(order,input.CouponCode,ct);
            repository.Add(order);
            await repository.SaveAsync(ct);
            order.Code = $"BS{order.CreatedAt.AddHours(7):yyyyMMdd}-{order.Id:D4}";
            foreach (var line in lines) { line.OrderId = order.Id; repository.Add(line); }
            repository.ClearCart(cart);
            await repository.SaveAsync(ct);
            return Map(order, lines);
        }, ct);

    public async Task<OrderDto> GetAsync(int id, int? userId, CancellationToken ct)
    {
        var order = await repository.FindAsync(id, userId, ct) ?? throw CatalogException.NotFound("Đơn hàng");
        return Map(order, await repository.LinesAsync(id, ct));
    }
    public async Task<PageDto<OrderDto>> ListAsync(int? userId, OrderQuery query, CancellationToken ct)
    {
        var page = await repository.ListAsync(userId, query, ct);
        var items = new List<OrderDto>();
        foreach (var order in page.Items) items.Add(Map(order, await repository.LinesAsync(order.Id, ct)));
        return new PageDto<OrderDto>(items, page.Page, page.PageSize, page.TotalItems);
    }
    public Task<OrderDto> TransitionAsync(int id, int? userId, string status, CancellationToken ct) =>
        repository.TransactionAsync(async () =>
        {
            var order = await repository.FindAsync(id, userId, ct) ?? throw CatalogException.NotFound("Đơn hàng");
            if ((userId.HasValue && order.Status != "Pending") || !OrderRules.CanTransition(order.Status, status))
                throw CatalogException.BadRequest("Không được chuyển trạng thái đơn hàng theo luồng này.");
            if (order.PaymentMethod == "VNPAY" && status == "Cancelled" && order.PaymentStatus == "Paid")
                throw CatalogException.BadRequest("Đơn đã thanh toán cần xử lý hoàn tiền trước khi hủy.");
            if (order.PaymentMethod == "VNPAY" && status is "Shipping" or "Completed" && order.PaymentStatus != "Paid")
                throw CatalogException.BadRequest("Chỉ giao hàng sau khi VNPay xác nhận đã thanh toán.");
            var lines = await repository.LinesAsync(id, ct);
            if (status == "Cancelled")
            {
                await coupons.ReleaseAsync(order,ct);
                var products = await repository.ProductsAsync(lines.Select(x => x.ProductId), ct);
                foreach (var line in lines)
                {
                    var product = products.Single(x => x.Id == line.ProductId);
                    product.Stock = checked(product.Stock + line.Quantity);
                }
            }
            order.Status = status;
            await repository.SaveAsync(ct);
            return Map(order, lines);
        }, ct);
    private static OrderDto Map(Order order, IEnumerable<OrderItem> lines) => new(order.Id, order.Code, order.Status,
        order.TotalAmount, order.ShippingAddress, order.Phone, order.PaymentMethod, order.Note, order.CreatedAt,
        lines.Select(x => new OrderLineDto(x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToList(), order.FullName, order.PaymentStatus, order.PaymentExpiredAt, order.CouponCode, order.DiscountAmount);
}

