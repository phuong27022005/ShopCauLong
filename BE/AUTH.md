# Đăng ký và đăng nhập

Frontend dùng sẵn `/login` và `/register`; Vite proxy `/api` đến backend cổng 5132.
Đăng ký thành công chuyển về đăng nhập. Tài khoản mới luôn có role `Customer`.
Email được chuẩn hóa về chữ thường, mật khẩu được băm bằng ASP.NET Core PasswordHasher.

## Chạy cục bộ

Tại thư mục `BE`, khôi phục công cụ và cập nhật SQL Server LocalDB:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet tool run dotnet-ef database update --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api
```

Cấu hình khóa JWT ngẫu nhiên (ít nhất 32 byte) bằng user-secrets:

```powershell
$jwtKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project BadmintonShop.Api
dotnet run --project BadmintonShop.Api --launch-profile http
```

Máy phát triển hiện tại đã có khóa ngẫu nhiên trong `BadmintonShop.Api/appsettings.Development.local.json`,
file này được Git bỏ qua và chỉ được nạp trong môi trường Development. Nếu dùng user-secrets thay thế,
bỏ khóa trong file local để tránh ghi đè. Không đưa khóa thật lên Git.

Tại thư mục `FE`, chạy `npm ci` và `npm run dev`, mở `http://localhost:5173/register`.
Tài khoản mẫu trong seed không có mật khẩu được cung cấp để đăng nhập; hãy đăng ký tài khoản mới.

## Phiên và triển khai

- Access token HS256 có hạn 15 phút, frontend dùng Bearer header.
- Refresh token ngẫu nhiên có hạn tuyệt đối 7 ngày, database chỉ lưu SHA-256.
- Cookie refresh dùng HttpOnly, SameSite=Strict và Secure khi truy cập HTTPS.
- Refresh thay token nguyên tử, token cũ không dùng lại được; logout thu hồi refresh token.
- Access token đã cấp vẫn có hiệu lực tối đa 15 phút sau logout.
- Các endpoint auth giới hạn 30 yêu cầu/phút/IP, trả 429 khi vượt giới hạn.
- Production cần `Jwt__SigningKey` riêng và HTTPS; phục vụ frontend/API cùng origin qua reverse proxy.
  Nếu TLS kết thúc tại proxy, cấu hình forwarded headers từ proxy tin cậy để backend nhận đúng HTTPS/IP.
- Không hỗ trợ cookie xác thực cross-site trong cấu hình mặc định.

Kiểm tra: `dotnet test BadmintonShop.sln`; frontend: `npm test` và `npm run build`.

## Xác minh email và đặt lại mật khẩu

- Đăng ký lưu tài khoản, token xác minh và email chờ gửi trong cùng giao dịch database.
- `POST /api/auth/resend-verification` nhận `{ email }`, gửi lại liên kết xác minh 24 giờ.
- `POST /api/auth/verify-email` nhận `{ token }`, xác minh một lần.
- `POST /api/auth/forgot-password` nhận `{ email }`, luôn phản hồi chung để không tiết lộ email đã đăng ký.
- `POST /api/auth/reset-password` nhận `{ token, password }`, liên kết hết hạn sau 30 phút.
- Token hành động lưu SHA-256. Reset mật khẩu vô hiệu hóa refresh token và access token cũ trên mọi thiết bị bằng TokenVersion.
- Liên kết chứa token trong URL fragment (`#token=`), không nằm trong query gửi lên web server.
- Mỗi loại email được giới hạn một lần/phút/tài khoản. Giới hạn HTTP theo IP vẫn áp dụng.
- Xác minh email hiện là trạng thái tài khoản hiển thị ở `/account`; chưa bắt buộc xác minh trước khi đăng nhập/mua hàng, để giữ hành vi của tài khoản hiện có.

### Email thử nghiệm trên máy

Trong Development, nếu `Email:SmtpHost` trống, worker tạo file `.eml` trong
`BE/BadmintonShop.Api/.local-mail/` sau khoảng 10 giây. Mở file bằng ứng dụng đọc email
để xem liên kết. Không gửi ra địa chỉ email thật ở chế độ này. Thư mục được Git bỏ qua.
`Email:FrontendUrl` mặc định là `http://localhost:5173`; dùng cùng hostname khi mở frontend để giữ phiên trình duyệt nhất quán.

### Gửi email thật

Cấu hình bằng user-secrets trong Development hoặc biến môi trường trên máy chủ:

| Biến môi trường | Giá trị |
| --- | --- |
| `Email__FrontendUrl` | Origin HTTPS của frontend, không lấy từ header yêu cầu |
| `Email__From` | Địa chỉ người gửi đã được nhà cung cấp cho phép |
| `Email__SmtpHost` | Máy chủ SMTP của nhà cung cấp |
| `Email__SmtpPort` | Cổng hỗ trợ STARTTLS, mặc định 587 |
| `Email__SmtpUser` | Tài khoản SMTP |
| `Email__SmtpPassword` | Mật khẩu ứng dụng/SMTP secret |

Production không tự chuyển sang lưu file khi thiếu SMTP. Worker ghi nhận lỗi và thử lại với thời gian chờ tăng dần,
tối đa 10 lượt. Email đã gửi được xóa nội dung liên kết khỏi bảng `OutgoingEmails`.
Cơ chế gửi là at-least-once: nếu tiến trình dừng ngay sau khi SMTP nhận thư, có thể gửi lại thư ở lần chạy sau.
Token vẫn chỉ tiêu thụ được một lần. Không lưu SMTP secret vào appsettings được theo dõi bởi Git.

Tắt worker trong môi trường test bằng `Email__WorkerEnabled=false`.
