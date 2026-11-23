# BadmintonShop.Web

Chạy `npm ci`, `npm run dev`, `npm run test` và `npm run build`.

FE mặc định gửi yêu cầu đến `/api`. Vite proxy đường dẫn này sang
`http://localhost:5132` khi phát triển. Có thể đặt `VITE_API_BASE_URL`
để dùng một địa chỉ khác. Khi triển khai, cấu hình máy chủ để hỗ trợ
các đường dẫn React Router và proxy `/api` nếu chạy cùng origin.

## Xác thực

BE đã triển khai các endpoint dưới đây. Các phản hồi JSON có dạng
`{ success, data, message }`. Xem `../BE/AUTH.md` để cấu hình và cập nhật database.

| Endpoint | Dữ liệu gửi | Dữ liệu nhận khi thành công |
| --- | --- | --- |
| POST /api/auth/login | `{ email, password }` | `{ user, accessToken }` và cookie refresh |
| POST /api/auth/register | `{ fullName, email, password }` | `null`; FE chuyển sang đăng nhập |
| GET /api/auth/me | Header Bearer | `{ id, fullName, email, role }` |
| POST /api/auth/refresh | Cookie refresh | `{ accessToken }` |
| POST /api/auth/logout | Cookie refresh và header Bearer nếu có | Thu hồi phiên refresh và xóa cookie |

Cookie refresh do BE đặt, sử dụng HttpOnly. FE lưu access token
trong localStorage để khôi phục phiên qua `/me`. API khác origin cần
cấu hình CORS có credentials và thuộc tính cookie phù hợp. API phải tự
xác thực và phân quyền cho các endpoint được bảo vệ.

Quy tắc đăng ký: họ tên 1–150 ký tự, email hợp lệ tối đa 254
ký tự, mật khẩu 8–128 ký tự có chữ thường, chữ hoa, số và ký tự đặc
biệt. Xác nhận mật khẩu chỉ dùng phía FE. Đăng nhập yêu cầu email hợp
lệ và mật khẩu không rỗng, tối đa 128 ký tự.

## Các màn hình bổ sung

- `/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification`: khôi phục mật khẩu và xác minh email.
- `/compare`: so sánh tối đa ba mẫu vợt; `/favorites`: danh sách yêu thích theo tài khoản.
- `/admin/inventory`: tồn khả dụng và lượng đang giữ trong đơn hàng.
- Form quản trị sản phẩm có thư viện ảnh; trang chi tiết có phóng to ảnh và trường trình độ phù hợp.
- Chi tiết đơn và quản trị đơn có lịch sử thay đổi trạng thái.

Xem [hướng dẫn tính năng](../BE/FEATURES.md) và [cấu hình email](../BE/AUTH.md).
