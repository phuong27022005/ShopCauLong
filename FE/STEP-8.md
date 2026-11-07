# Bước 8 – Hoàn thiện giao diện

## Chức năng
- Chi tiết sản phẩm: sao trung bình, danh sách review phân trang; quyền hiển thị form do server xác nhận. Chọn sao bắt buộc; gửi xong tải lại danh sách và điều kiện.
- Giỏ/checkout dùng chung CouponContext: áp dụng, xóa mã, thông báo lỗi BE, tạm tính/giảm giá/tổng. Đổi giỏ hoặc người dùng làm mất hiệu lực mã cũ; kết quả request cũ bị bỏ qua.
- Checkout chỉ gửi couponCode, không gửi số tiền giảm; BE vẫn tính lại khi đặt đơn.
- /admin được bảo vệ bằng AdminRoute, có sidebar trên desktop và menu co giãn trên mobile.
- Dashboard: thẻ số liệu, trạng thái đơn, biểu đồ Recharts tải riêng khi mở dashboard, bảng top sản phẩm; lọc ngày và nhóm day/month, trạng thái loading/error/rỗng.
- /admin/products: tìm kiếm, phân trang, thêm/sửa, ẩn/hiện cả sản phẩm đã ẩn; form zod/react-hook-form, kiểm tra MIME/đuôi ảnh/dung lượng 2 MB, preview, upload multipart. Giữ ID sau lần lưu đầu để retry upload không tạo sản phẩm trùng.
- /admin/orders: lọc, phân trang, chọn bước chuyển hợp lệ. Không hiện hủy đơn VNPay đã Paid theo quy tắc BE.
- /admin/coupons: danh sách, thêm/sửa, xóa có xác nhận, validate mã/giá trị/ngày; chuyển thời gian nhập cục bộ sang ISO UTC.
- /admin/reviews: phân trang, điểm sao/nội dung và xóa có xác nhận.

## API đọc bổ sung để FE hoạt động đầy đủ
Không đổi schema, không cần migration:
- GET /api/admin/products: AdminOnly, phân trang/tìm kiếm, trả ProductDetailDto gồm cả sản phẩm ẩn.
- GET /api/admin/reviews: AdminOnly, phân trang tất cả review.
- GET /api/products/{productId}/reviews/eligibility: Authorize, lấy userId từ token; trả canReview/reason từ đơn Completed và review hiện có.
API public products vẫn chỉ trả sản phẩm đang bán.

## File FE tạo
- src/shared/useApiResource.ts
- src/shared/ResourceState.tsx
- src/coupons/CouponContext.tsx
- src/coupons/CouponBox.tsx
- src/reviews/Reviews.tsx
- src/admin/AdminLayout.tsx
- src/admin/Dashboard.tsx
- src/admin/AdminProducts.tsx
- src/admin/ProductForm.tsx
- src/admin/AdminOrders.tsx
- src/admin/AdminCoupons.tsx
- src/admin/AdminReviews.tsx
- src/test/engagement.test.tsx
- src/test/admin.test.tsx
- STEP-8.md

## File FE sửa
- package.json, package-lock.json: thêm Recharts.
- src/App.tsx: provider, route Admin, lazy dashboard.
- src/components/Header.tsx: link quản trị cho Admin.
- src/pages/ProductDetailPage.tsx: review.
- src/pages/CartPage.tsx, src/pages/CheckoutPage.tsx: coupon.
- src/styles.css: input, bảng và trạng thái disable.
- src/test/server.ts: mock mặc định các API đọc mới.
- src/test/setup.ts: ResizeObserver cho layout SVG, URL blob preview, timeout chờ dynamic import.

## File BE tạo
- BadmintonShop.Api/Controllers/AdminReadController.cs

## File BE sửa
- BadmintonShop.Core/Interfaces/ICatalogRepository.cs
- BadmintonShop.Core/Interfaces/IEngagementRepository.cs
- BadmintonShop.Infrastructure/Repositories/CatalogRepository.cs
- BadmintonShop.Infrastructure/Repositories/ProductQueryBuilder.cs
- BadmintonShop.Infrastructure/Repositories/EngagementRepository.cs
- BadmintonShop.Api/Catalog/CatalogService.cs
- BadmintonShop.Api/Engagement/ReviewService.cs
- BadmintonShop.Api/Controllers/EngagementControllers.cs
- BadmintonShop.Tests/EngagementTests.cs

## Lệnh và kết quả
Trong FE:
- npm install recharts: thành công.
- npm run test: 77/77 pass, 8 file test, gồm 20 test mới.
- npm run build: TypeScript + Vite thành công.
Trong BE:
- dotnet test --no-restore -m:1 /nodeReuse:false -p:UseSharedCompilation=false: 129/129 pass, gồm 2 test mới; lệnh này build lại solution trước khi chạy test.

Môi trường test:
- FE TEMP/TMP trỏ vào node_modules/.cache/vitest-temp.
- BE DOTNET_CLI_HOME=D:/DuAnShopCauLong/.dotnet-local, NUGET_PACKAGES=D:/DuAnShopCauLong/.nuget-local.
- Tổng hồi quy tất cả các bước: 206/206 test pass, không skip.
- FE dùng RTL + MSW/jsdom; biểu đồ render SVG Recharts thật với ResizeObserver mô phỏng. Multipart kiểm tra request MSW và File truyền vào FormData vì jsdom/MSW đổi File thành blob ở ranh giới Request.
- BE dùng integration SQLite + JWT. Chưa chạy end-to-end trên SQL Server/browser thật.
- Build có cảnh báo annotation dependency Zod và bundle chính trên 500 kB; build vẫn thành công, dashboard/Recharts đã tách chunk.

