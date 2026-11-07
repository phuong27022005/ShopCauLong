# Bước 8A: đánh giá và coupon

## API
Tất cả response dùng envelope `{success,data,message}`.

- GET /api/products/{productId}/reviews?page=1&pageSize=12: phân trang, AverageRating tính trên tất cả review.
- POST /api/products/{productId}/reviews: {rating,comment}; cần đơn Completed.
- PUT/DELETE /api/products/{productId}/reviews/{id}: chủ review; Admin được xóa.
- POST /api/coupons/validate: {code,subtotal}; chỉ xem trước, lúc tạo đơn luôn tính lại từ Product.
- GET/POST /api/admin/coupons; GET/PUT/DELETE /api/admin/coupons/{id}: AdminOnly.
- POST /api/orders nhận thêm couponCode tùy chọn. Response đơn thêm couponCode, discountAmount.

Coupon chuẩn hóa chữ hoa, unique ở DB. RowVersion bảo vệ lượt dùng; cập nhật coupon, tồn kho, đơn và giỏ cùng transaction.
Hủy đơn thủ công hoặc job VNPay hoàn lượt dùng cùng transaction. Coupon đã gắn với đơn chỉ bị tắt khi xóa để giữ lịch sử.
Ngày coupon dùng UTC; EndAt là thời điểm hết hiệu lực.

## File tạo
- BadmintonShop.Core/Entities/Review.cs
- BadmintonShop.Core/Entities/Coupon.cs
- BadmintonShop.Core/DTOs/EngagementDtos.cs
- BadmintonShop.Core/Interfaces/IEngagementRepository.cs
- BadmintonShop.Infrastructure/Repositories/EngagementRepository.cs
- BadmintonShop.Api/Engagement/ReviewService.cs
- BadmintonShop.Api/Engagement/CouponService.cs
- BadmintonShop.Api/Engagement/EngagementValidators.cs
- BadmintonShop.Api/Controllers/EngagementControllers.cs
- BadmintonShop.Infrastructure/Data/Migrations/20261106100044_ReviewsAndCoupons.cs
- BadmintonShop.Infrastructure/Data/Migrations/20261106100044_ReviewsAndCoupons.Designer.cs
- BadmintonShop.Tests/EngagementTests.cs
- STEP-8A.md

## File sửa
- BadmintonShop.Core/Entities/Order.cs
- BadmintonShop.Core/DTOs/OrderDtos.cs
- BadmintonShop.Api/Orders/OrderService.cs
- BadmintonShop.Api/Orders/OrderValidators.cs
- BadmintonShop.Api/Payments/PaymentExpiration.cs
- BadmintonShop.Api/Program.cs
- BadmintonShop.Api/Catalog/CatalogErrorMiddleware.cs
- BadmintonShop.Infrastructure/Data/ShopDbContext.cs
- BadmintonShop.Infrastructure/Data/Migrations/ShopDbContextModelSnapshot.cs
- BadmintonShop.Tests/OrderTests.cs (mở rộng rollback để kiểm tra lượt coupon)

## Kiểm chứng
Trong BE:
- dotnet build --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false: thành công, 0 warning/error.
- dotnet test --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false --filter FullyQualifiedName~EngagementTests: 17/17.
- dotnet test --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false: 113/113, không skip.
- dotnet ef migrations has-pending-model-changes --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api --no-build: không còn thay đổi model.

Trong FE (hồi quy, không sửa source):
- npm run test: 57/57, 6 file test.
- npm run build: thành công; có cảnh báo annotation của dependency Zod.

Tổng: 170/170 test pass. Integration test dùng SQLite; chưa chạy trên SQL Server thật.
Migration đã sinh nhưng chưa áp dụng vào SQL Server. Khi cấu hình connection string, chạy:
dotnet ef database update --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api

