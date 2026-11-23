# Chức năng mở rộng cửa hàng

## Cập nhật và chạy

```powershell
# Tại thư mục BE
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet tool run dotnet-ef database update --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api
dotnet run --project BadmintonShop.Api --launch-profile http
# Tại thư mục FE, trong terminal khác
npm run dev
```

Migration `ShoppingAndAccountFeatures` thêm trạng thái email, phiên bản token, trình độ phù hợp của vợt,
token hành động, hàng đợi email, yêu thích và lịch sử đơn hàng. Giữ nguyên các migration/ảnh sản phẩm đã có.
LocalDB trên máy hiện tại đã được cập nhật. Cấu hình JWT/email xem [AUTH.md](AUTH.md).

## Các luồng demo

1. Đăng ký rồi mở email trong `.local-mail` để xác minh. Trang Tài khoản hiển thị trạng thái xác minh.
2. Chọn Quên mật khẩu, mở liên kết trong email, đặt mật khẩu mới. Phiên cũ không còn dùng được.
3. Chọn So sánh ở thẻ sản phẩm hoặc trang chi tiết. Bảng `/compare` so sánh tối đa 3 mẫu theo giá,
   thương hiệu, trọng lượng, độ cứng, điểm cân bằng, trình độ và tồn kho. Lựa chọn lưu trên trình duyệt;
   dữ liệu giá/tồn kho được lấy lại từ API, không tin bản sao cũ trong localStorage.
4. Đăng nhập, nhấn Yêu thích và mở `/favorites`. Danh sách lưu theo tài khoản, không chia sẻ giữa người dùng.
5. Quản trị → Sản phẩm → Sửa → Thư viện ảnh. Tải nhiều ảnh, đặt đại diện, bỏ ảnh khỏi thư viện.
   Mỗi sản phẩm tối đa 8 ảnh; mỗi file JPG/PNG/WebP tối đa 2 MB; backend kiểm tra chữ ký file.
   Ảnh mới dùng tên ngẫu nhiên. Bỏ khỏi thư viện không xóa file vật lý vì có thể được sản phẩm khác dùng chung.
   Trang chi tiết cho phép chọn thumbnail, phóng to bằng hộp thoại và đóng bằng Escape.
6. Quản trị → Tồn kho hiển thị khả dụng và lượng đang giữ. Khi đặt hàng, tồn khả dụng trừ ngay trong transaction;
   rowversion ngăn hai lần checkout ghi đè tồn kho. Hủy đơn hoàn tồn. Đơn VNPay chưa trả tiền hết hạn sau 15 phút,
   worker kiểm tra mỗi 30 giây, tự hủy và hoàn tồn/mã giảm giá đúng một lần.
   Đơn VNPay chưa thanh toán không được chuyển sang Giao hàng/Hoàn thành.
7. Khách mở Chi tiết đơn hoặc quản trị chọn Xem lịch sử. Nhật ký ghi trạng thái trước/sau, thời gian và người thực hiện;
   tác vụ tự động ghi Hệ thống. Nhật ký cùng transaction với đơn, không lưu sự kiện của giao dịch bị rollback.
   Chỉ chủ đơn hoặc quản trị được xem. Các đơn cũ không được dựng lịch sử giả; lịch sử bắt đầu từ khi triển khai.

## API bổ sung

| API | Quyền |
| --- | --- |
| `GET /api/discovery/categories` | Công khai; chỉ danh mục có sản phẩm đang bán và còn hàng |
| `GET /api/products/compare?ids=1&ids=2` | Công khai; tối đa 3 ID khác nhau; không trả sản phẩm ẩn |
| `GET /api/favorites` | Người đăng nhập |
| `PUT /api/favorites/{productId}` | Người đăng nhập; thao tác lặp không tạo bản ghi trùng |
| `DELETE /api/favorites/{productId}` | Người đăng nhập; chỉ tác động danh sách của mình |
| `GET/POST /api/products/{id}/gallery` | Quản trị; POST multipart với trường file |
| `PUT /api/products/{id}/gallery/cover` | Quản trị; JSON `{ url }`, ảnh phải thuộc sản phẩm |
| `DELETE /api/products/{id}/gallery` | Quản trị; JSON `{ url }` |
| `GET /api/admin/inventory?page=1&pageSize=12&search=Yonex` | Quản trị |
| `GET /api/orders/{id}/history` | Chủ đơn hoặc quản trị |
| `GET /api/admin/orders/{id}/history` | Kiểm tra quyền theo đơn như trên |

## Hình ảnh và thông số

Banner sân cầu lông và các hình minh họa trạng thái được dựng bằng SVG trong mã nguồn.
Ảnh sản phẩm hiện có được giữ nguyên. Danh mục lấy ảnh từ sản phẩm thực tế trong database,
không hiển thị giày/túi/phụ kiện khi chưa có sản phẩm tương ứng.
Trường `SkillLevel` được quản trị cập nhật ở form sản phẩm; dữ liệu chưa có hiển thị Chưa cập nhật,
không suy đoán thông số hoặc trình độ từ tên vợt.

## Kiểm thử

`dotnet test BadmintonShop.sln` kiểm tra cả API và nghiệp vụ với SQLite, bao gồm token dùng một lần,
thu hồi phiên sau reset, giới hạn email, quyền yêu thích/lịch sử, upload ảnh, giao đơn chưa trả tiền,
hoàn tồn đúng một lần và rollback nhật ký. Migration được chạy trên SQL Server LocalDB.

`npm test` kiểm tra giao diện bằng Vitest/MSW; `npm run build` kiểm tra TypeScript và build production.
So sánh và thư viện ảnh cũng được kiểm tra trực tiếp trên trình duyệt với API/LocalDB.
SMTP thật và giao dịch VNPay thật cần thông tin nhà cung cấp, không nằm trong kiểm thử cục bộ.
