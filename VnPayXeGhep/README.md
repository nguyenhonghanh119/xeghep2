# XeGhép — Bản chuyển đổi PHP → ASP.NET Core (C#)

Dự án này chuyển toàn bộ phần **hành khách** (`khach-index.php`, `khach-dat-cho.php`,
`khach-chuyen-cua-toi.php`, `khach-ho-so.php`) và luồng thanh toán **VNPay**
(`config.php`, `db.php`, `vnpay_create_payment.php`, `vnpay_return.php`, `vnpay_ipn.php`)
sang một ứng dụng ASP.NET Core MVC (.NET 8) hoàn chỉnh, nối thẳng vào MySQL theo
đúng schema `datxeghep.sql`. Giao diện giữ nguyên `style.css` gốc.

## 1. Cài đặt

1. Cài .NET 8 SDK: https://dotnet.microsoft.com/download
2. Cài MySQL 8 (hoặc MariaDB), sau đó import schema:
   ```bash
   mysql -u root -p < datxeghep.sql
   ```
3. Mở `appsettings.json`, sửa `ConnectionStrings:Default` cho khớp user/mật khẩu MySQL của bạn
   (mặc định đang để giống `db.php` gốc: user `xeghep_db`, mật khẩu `hanh`).
4. Chạy:
   ```bash
   dotnet restore
   dotnet run
   ```
5. Mở trình duyệt: `http://localhost:5000/khach-index` (hoặc cổng dotnet in ra trong console).
   Swagger UI (test API) tại `http://localhost:5000/swagger`.

## 2. Đăng nhập giả lập

Bản demo PHP gốc giả lập hành khách đăng nhập bằng `db.php` (`$driver_id = 2`).
Ở bản C# này, hành khách được giả lập là **Nguyễn Thị Lan (user_id = 5)** —
xem hằng số `DemoSession.CurrentPassengerId` trong `Controllers/PassengerController.cs`.
Khi bạn có hệ thống đăng nhập thật, thay hằng số này bằng `user_id` lấy từ session/JWT.

## 3. Cấu trúc dự án — ánh xạ từ PHP sang C#

| File PHP gốc | Thành phần C# tương ứng |
|---|---|
| `config.php` (biến `$vnp_*`) | `Models/VnPayOptions.cs` + section `VnPay` trong `appsettings.json` |
| `db.php` (kết nối PDO) | `Data/AppDbContext.cs` (EF Core + Pomelo MySQL) |
| `vnpay_create_payment.php`, `khach-thanh-toan.php` | `Services/VnPayService.cs` (`BuildPaymentUrl`) + `PassengerService.CreateBookingAsync` |
| `vnpay_return.php` | `Controllers/VnPayController.cs` → action `Return` + view `Views/VnPay/VnPayResult.cshtml` |
| `vnpay_ipn.php` | `Controllers/VnPayController.cs` → action `Ipn` (trả đúng `RspCode` theo chuẩn VNPay) |
| `khach-index.php` | `Controllers/PassengerController.cs` → `Index` + `Views/Passenger/Index.cshtml` |
| `khach-dat-cho.php` | → `DatCho` + `Views/Passenger/DatCho.cshtml` |
| `khach-chuyen-cua-toi.php` | → `ChuyenCuaToi` + `Views/Passenger/ChuyenCuaToi.cshtml` |
| `khach-ho-so.php` | → `HoSo` + `Views/Passenger/HoSo.cshtml` |
| Toàn bộ `// TODO BACKEND` trong các file `.php` | Đã được cài đặt thật trong `Controllers/PassengerApiController.cs` (`/api/passenger/...`) và `Services/PassengerService.cs` |

## 4. Những gì đã được nối "thật" (không còn demo/alert giả)

- **Tìm chuyến**: đọc trực tiếp bảng `trips` (chỉ trip còn ghế trống, trạng thái `upcoming`),
  lọc theo điểm đi/đến/ngày/số ghế.
- **Đặt chỗ**: giá vé & số ghế trống luôn lấy từ DB (không tin số liệu client gửi lên,
  đúng như ghi chú TODO quan trọng trong `khach-thanh-toan.php` gốc). Tạo `bookings`,
  trừ `available_seats` của `trips`.
  - Thanh toán **tiền mặt**: đặt `payment_status = pending_cash`, duyệt ngay (demo).
  - Thanh toán **online**: dựng URL VNPay thật (ký `HMAC-SHA512` giống hệt code PHP gốc)
    và redirect sang sandbox VNPay.
- **VNPay return/IPN**: kiểm tra chữ ký, đối chiếu số tiền, cập nhật `bookings.payment_status`,
  tạo bản ghi `transactions` (trừ hoa hồng theo `system_settings.commission_rate`),
  cộng ví tài xế (`driver_profiles.wallet_balance`) nếu `system_settings.auto_payout = true`.
- **Chuyến của tôi**: đọc `bookings` join `trips`, tự phân vào 4 tab theo trạng thái
  chuyến/đặt chỗ. Nút **Huỷ chuyến** cập nhật `bookings.status = cancelled`, hoàn lại
  ghế trống cho `trips.available_seats`, đổi `payment_status` sang `refunded` nếu đã trả online.
  Nút **Đánh giá tài xế** ghi vào bảng `reviews` và tính lại `driver_profiles.rating` trung bình.
- **Hồ sơ & Thanh toán**: sửa thông tin cá nhân (`users`), thêm/xoá phương thức thanh toán
  (`passenger_payment_methods`), xem lịch sử giao dịch thật từ `bookings`.

## 5. Lưu ý triển khai thực tế

- Đổi `ExpireMinutes`/`ReturnUrl` trong `appsettings.json` cho đúng domain thật khi deploy.
- `TmnCode`/`HashSecret` hiện đang dùng giá trị sandbox demo giống file `config.php` gốc —
  thay bằng thông tin merchant thật khi lên production.
- Nên tắt Swagger hoặc bảo vệ bằng xác thực khi triển khai production (hiện đang bật ở mọi môi trường
  để tiện test, giống ghi chú trong `Program.cs` gốc).
- Thanh toán qua Momo/ZaloPay/Thẻ hiện chỉ ghi chú vào nội dung đơn hàng VNPay (giống bản PHP demo) —
  cần tích hợp SDK riêng của từng cổng nếu muốn dùng thật.
