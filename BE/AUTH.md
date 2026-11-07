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
