# Bước 8B – API thống kê Admin

## Quy ước
- Cả ba endpoint yêu cầu JWT và policy AdminOnly, trả envelope {success,data,message}.
- from/to là ngày YYYY-MM-DD, tùy chọn, theo CreatedAt UTC. to bao gồm cả ngày; bỏ trống là không giới hạn phía đó.
- Doanh thu dùng Order.TotalAmount sau coupon; đơn Completed HOẶC PaymentStatus Paid được tính một lần. Luôn loại Status Cancelled.
- Số đơn theo trạng thái gồm cả đơn chưa thanh toán và đã hủy trong khoảng ngày.
- Khách mới là User có Role Customer, CreatedAt trong khoảng ngày.
- Sắp hết hàng là sản phẩm đang bán (IsActive), Stock <= 5 tại thời điểm truy vấn; không phụ thuộc khoảng ngày.
- Revenue chỉ trả các kỳ có doanh thu đủ điều kiện, không tự chèn kỳ trống; month dùng ngày đầu tháng.
- Top sản phẩm tính số lượng OrderItem thuộc các đơn đủ điều kiện doanh thu, toàn thời gian, kể cả sản phẩm hiện bị ẩn. Đồng hạng sắp ProductId tăng dần.
- Tất cả lọc/nhóm/cộng/đếm/sắp xếp/Take chạy ở DB. Sau materialize chỉ định dạng ngày của các dòng đã tổng hợp.
- SQL Server dùng SUM(decimal); SQLite test dùng ROUND rồi SUM đơn vị 0,01 do EF Core 8 SQLite không hỗ trợ SUM(decimal).

## Endpoint
- GET /api/admin/stats/overview?from=2026-01-01&to=2026-01-31
  data: {totalRevenue,ordersByStatus:[{status,count}],newCustomers,lowStockProducts}
- GET /api/admin/stats/revenue?from=2026-01-01&to=2026-01-31&groupBy=day
  data: [{period:"2026-01-01",revenue:300.4}]; groupBy day (mặc định) hoặc month.
- GET /api/admin/stats/top-products?limit=10
  data: [{productId,name,quantitySold}]; limit 1..100, mặc định 10.

Khoảng ngày rỗng trả doanh thu/khách mới 0, ordersByStatus và revenue rỗng; tồn kho vẫn là số hiện tại.
from > to, ngày sai định dạng, groupBy hoặc limit không hợp lệ trả 400.

## File tạo
- BadmintonShop.Core/DTOs/StatsDtos.cs
- BadmintonShop.Core/Interfaces/IStatsRepository.cs
- BadmintonShop.Infrastructure/Repositories/StatsRepository.cs
- BadmintonShop.Api/Stats/StatsService.cs
- BadmintonShop.Api/Stats/StatsValidators.cs
- BadmintonShop.Api/Controllers/StatsController.cs
- BadmintonShop.Tests/StatsTests.cs
- BadmintonShop.Infrastructure/Data/Migrations/20261106102302_AdminStatsIndexes.cs
- BadmintonShop.Infrastructure/Data/Migrations/20261106102302_AdminStatsIndexes.Designer.cs
- STEP-8B.md

## File sửa
- BadmintonShop.Api/Program.cs: DI repository/service.
- BadmintonShop.Infrastructure/Data/ShopDbContext.cs: index Orders.CreatedAt, Users(Role,CreatedAt), Products(IsActive,Stock).
- BadmintonShop.Infrastructure/Data/Migrations/ShopDbContextModelSnapshot.cs.

## Kiểm chứng
Lệnh tại BE, môi trường DOTNET_CLI_HOME và NUGET_PACKAGES trỏ vào thư mục local của workspace:
- dotnet test --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false --filter FullyQualifiedName~StatsTests: 14/14 pass.
- dotnet build --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false: thành công, 0 warning/error.
- dotnet test --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false: 127/127 pass, 0 skip.
- dotnet ef migrations has-pending-model-changes --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api --no-build: model khớp migration.

Hồi quy toàn bộ FE:
- npm run test: 57/57 pass.
- npm run build: thành công (cảnh báo annotation từ dependency Zod).

Tổng 184/184 test pass. Integration chạy SQLite, chưa kiểm thử SQL Server thật.
Migration đã tạo, chưa áp dụng vào SQL Server. Sau khi cấu hình kết nối:
dotnet ef database update --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api

