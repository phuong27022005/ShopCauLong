# Thử VNPay sandbox

## Cấu hình

Chạy từ thư mục BE. Đăng ký merchant sandbox để nhận TmnCode và HashSecret.
Khóa thật chỉ đặt trong User Secrets hoặc biến môi trường, không điền vào
appsettings.json, source code, ảnh chụp màn hình hay Git.

```powershell
dotnet user-secrets set "Vnpay:TmnCode" "<TMN_CODE>" --project BadmintonShop.Api
dotnet user-secrets set "Vnpay:HashSecret" "<HASH_SECRET>" --project BadmintonShop.Api
dotnet user-secrets set "Vnpay:FrontendReturnUrl" "http://localhost:5173/payment/result" --project BadmintonShop.Api
dotnet ef database update --project BadmintonShop.Infrastructure --startup-project BadmintonShop.Api
dotnet dev-certs https --trust
dotnet run --project BadmintonShop.Api --launch-profile https
```

Cấu hình ConnectionStrings:DefaultConnection phù hợp với SQL Server của bạn.
Migration VnpayPayments tạo bảng giao dịch và thời hạn cho đơn VNPay cũ.
PaymentExpiredAt là UTC, đúng 15 phút sau CreatedAt. Job chạy mỗi 30 giây,
xử lý tối đa 100 đơn mỗi lượt, hủy đơn Pending/Confirmed chưa Paid và hoàn tồn kho.

## Ngrok và callback

Trong terminal khác, đăng nhập ngrok theo tài khoản của bạn rồi chạy:

```powershell
ngrok http https://localhost:7023
```

Ví dụ ngrok cấp https://example.ngrok-free.app. Dừng API, cập nhật rồi khởi động lại:

```powershell
dotnet user-secrets set "Vnpay:ReturnUrl" "https://example.ngrok-free.app/api/payments/vnpay/return" --project BadmintonShop.Api
dotnet user-secrets set "Vnpay:IpnUrl" "https://example.ngrok-free.app/api/payments/vnpay/ipn" --project BadmintonShop.Api
```

Đăng ký chính URL IPN này với merchant sandbox VNPay (qua cấu hình merchant
hoặc đầu mối hỗ trợ VNPay). Việc đặt IpnUrl trong ứng dụng không tự đăng ký
callback với VNPay. URL pay phiên bản 2.1.0 chỉ mang ReturnUrl.
Khi hostname ngrok thay đổi, cập nhật cả cấu hình ứng dụng và đăng ký IPN.
IPN phải truy cập công khai, không đặt màn hình đăng nhập/ngrok OAuth phía trước.

## Token local để thử API

Repo hiện chưa có endpoint đăng nhập BE. Có thể dùng JWT hợp lệ từ hệ thống
xác thực khi có sẵn. Để thử local ngay, cấu hình một khóa JWT riêng chỉ cho
máy phát triển, sau đó ký token Customer cho user seed ID 2 bằng PowerShell 7.

Đặt khóa đủ 32 byte trở lên trong Jwt:SigningKey của User Secrets, giữ trong
biến local bằng Read-Host; không dùng HashSecret VNPay làm khóa JWT.
Đoạn dưới tạo khóa ngẫu nhiên mới và lưu qua stdin để không đưa khóa vào
tham số dòng lệnh. Khởi động lại API sau khi thiết lập.

```powershell
$jwtDevKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
@{ 'Jwt:SigningKey' = $jwtDevKey } | ConvertTo-Json -Compress |
    dotnet user-secrets set --project BadmintonShop.Api

function To-Base64Url([byte[]] $bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
}
$jwtHeader = To-Base64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
$jwtPayload = @{
    sub = '2'
    role = 'Customer'
    iss = 'BadmintonShop'
    aud = 'BadmintonShop.Web'
    exp = [DateTimeOffset]::UtcNow.AddHours(1).ToUnixTimeSeconds()
} | ConvertTo-Json -Compress
$jwtBody = To-Base64Url ([Text.Encoding]::UTF8.GetBytes($jwtPayload))
$jwtUnsigned = "$jwtHeader.$jwtBody"
$jwtSignature = To-Base64Url ([Security.Cryptography.HMACSHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($jwtDevKey), [Text.Encoding]::UTF8.GetBytes($jwtUnsigned)))
$jwtHeaders = @{ Authorization = "Bearer $jwtUnsigned.$jwtSignature" }
```

## Tạo đơn và mở thanh toán

Dùng cùng terminal đang giữ $jwtHeaders, sau khi API đã khởi động lại:

```powershell
$apiBase = 'https://localhost:7023'
Invoke-RestMethod "$apiBase/api/cart/items" -Method Post -Headers $jwtHeaders -ContentType 'application/json' -Body '{"productId":1,"quantity":1}'
$orderResult = Invoke-RestMethod "$apiBase/api/orders" -Method Post -Headers $jwtHeaders -ContentType 'application/json' -Body '{"shippingAddress":"Ha Noi","phone":"0901234567","paymentMethod":"VNPAY","note":"Sandbox","fullName":"Nguyen Van A"}'
$paymentBody = @{ orderId = $orderResult.data.id } | ConvertTo-Json
$paymentResult = Invoke-RestMethod "$apiBase/api/payments/vnpay/create" -Method Post -Headers $jwtHeaders -ContentType 'application/json' -Body $paymentBody
Start-Process $paymentResult.data.paymentUrl
```

Chọn thẻ ATM/tài khoản ngân hàng NCB:

- Số thẻ thành công: 9704198526191432198
- Tên chủ thẻ: NGUYEN VAN A
- Ngày phát hành: 07/15
- OTP: 123456
- Thẻ thử không đủ số dư: 9704195798459170488, cùng tên/ngày phát hành.

Return chuyển về FE /payment/result?orderId=...&paymentResult=... khi mã giao
dịch có chữ ký hợp lệ. Trang kết quả đọc trạng thái đơn từ API và kiểm tra lại
tối đa 6 lần, cách nhau 3 giây nếu còn Unpaid. Return không thay đổi dữ liệu.
Nếu trước đây đã đặt FrontendReturnUrl trong User Secrets về /orders,
cần đổi thành http://localhost:5173/payment/result vì User Secrets ghi đè appsettings.

Kiểm tra ngrok inspector (thường ở http://127.0.0.1:4040) và GET đơn:

```powershell
Invoke-RestMethod "$apiBase/api/orders/$($orderResult.data.id)" -Headers $jwtHeaders
```

Chỉ sau IPN hợp lệ, paymentStatus mới là Paid. Bảng PaymentTransactions có
một bản ghi cho mỗi lần tạo URL, ResponseCode/RawData được điền sau IPN.
IPN lặp trả 02, không tạo thêm bản ghi. IPN sai chữ ký trả 97, không tìm thấy
giao dịch trả 01, sai tiền trả 04. 00 nghĩa là đã ghi nhận kết quả callback,
kể cả callback báo giao dịch thất bại. Đơn Failed có thể tạo URL mới trước hạn.

Để thử hết hạn, tạo đơn VNPay rồi không thanh toán. Chờ ít nhất 15 phút
cộng chu kỳ job, kiểm tra đơn Cancelled và tồn kho đã hoàn. IPN đến sau khi
đơn đóng/hết hạn trả 99 và không đánh dấu Paid. Nếu ngân hàng đã trừ tiền
trong trường hợp callback quá muộn, cần đối soát với VNPay; chưa tự hoàn tiền.
PaymentStatus Refunded được dự phòng cho quy trình hoàn tiền, không có
API hoàn tiền trong bước này. Đơn Paid bị chặn hủy trực tiếp.

## Tài liệu đối chiếu

- https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html
- https://sandbox.vnpayment.vn/apis/vnpay-demo/
- https://ngrok.com/docs/gateway/endpoints/http

Test tự động dùng khóa giả rõ tên trong test, callback HTTP giả lập và SQLite.
Chưa có giao dịch sandbox thật vì chưa cấu hình merchant của bạn.

