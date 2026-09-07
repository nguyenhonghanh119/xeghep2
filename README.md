# XeGhép — Quản trị (chuyển từ PHP sang C# ASP.NET Core Razor Pages)

## Cách chạy
Cần cài .NET 8 SDK, sau đó trong thư mục này:

```
dotnet restore
dotnet run
```

Mở trình duyệt tại địa chỉ được in ra (vd `https://localhost:5001`). Trang đầu tiên là Dashboard (`/`).

## Cấu trúc dự án
- `Program.cs` — điểm khởi động, đăng ký Razor Pages + `SampleDataStore` (dữ liệu mẫu, thay cho mảng hard-code trong PHP gốc).
- `Models/` — các lớp dữ liệu (Driver đã gộp Vehicle, Customer, Trip, Booking, Review/Complaint, GiaoDich/YeuCauRutTien, StaffAccount, TuyenGia/CauHinhGia, v.v.)
- `Data/SampleDataStore.cs` — dữ liệu demo trong bộ nhớ, tương đương các biến/mảng hard-code trong từng file `admin-*.php` gốc. **Xoá file này khi nối API/DB thật.**
- `Pages/` — mỗi trang admin cũ (`admin-xxx.php`) nay là 1 cặp `Xxx.cshtml` (giao diện, giữ nguyên 100% HTML/CSS gốc) + `Xxx.cshtml.cs` (PageModel — logic, tương đương phần code PHP ở đầu file).
- `Pages/Shared/_Layout.cshtml` + `_Sidebar.cshtml` — thay cho `partials/sidebar.php`, dùng chung cho mọi trang.
- `wwwroot/css/style.css` — **giữ nguyên không đổi** từ file gốc bạn cung cấp, để đảm bảo giao diện y hệt.
- `wwwroot/js/common.js` — JS dùng chung (đóng/mở menu con sidebar, format tiền VNĐ...), thay cho `assets/common.js` (không có trong bộ file gốc bạn gửi nên được viết lại tối giản).

Mỗi PageModel đều giữ nguyên các khối comment `// TODO BACKEND: ...` liệt kê endpoint API gợi ý, y hệt tinh thần các khối `TODO BACKEND` trong PHP gốc — bạn chỉ cần thay `SampleDataStore` bằng service gọi API/DB thật.

## Danh sách trang sau khi chỉnh sửa theo yêu cầu

| Trang | Route | Ghi chú |
|---|---|---|
| Dashboard | `/` | Không đổi |
| Khách hàng | `/KhachHang` | Không đổi (đổi tên file cho khớp activePage gốc) |
| **Tài xế** | `/TaiXe` | **Đã gộp "Quản lý phương tiện"**: mỗi tài xế hiển thị luôn biển số, loại xe, số chỗ, hạn đăng kiểm (cảnh báo khi sắp hết hạn); nút "Xem giấy tờ" mở hồ sơ tài xế **+** phương tiện cùng lúc. Duyệt hồ sơ tài xế = duyệt luôn phương tiện đi kèm. |
| **Tài khoản mới** | `/TaiKhoan` | **MỚI** — danh sách tài khoản khách hàng/tài xế do hệ thống **tự động tạo thành công** khi người dùng tự đăng ký qua app. |
| Chuyến xe | `/ChuyenXe` | Không đổi |
| Đặt xe | `/DatXe` | Không đổi |
| **Đăng chuyến & Giá** | `/DangChuyen` | Thay cho 2 trang cũ **"Quản lý tuyến đường" + "Quản lý giá"** đã gộp làm một: cấu hình giá mặc định, form "Đăng chuyến" có **giá đề xuất tính & hiển thị ngay bên cạnh** (JS live-preview theo khoảng cách km), và bảng danh sách tuyến kèm giá. |
| Đánh giá & khiếu nại | `/DanhGia` | Không đổi |
| **Thanh toán** | `/ThanhToan` | **Rút tiền tài xế nay tự động**: bật công tắc "Rút tiền tự động cho tài xế" + hạn mức tự động duyệt; danh sách "Yêu cầu rút tiền chờ duyệt" đổi thành **"Lịch sử rút tiền (tự động xử lý)"** — chỉ còn hiển thị các lần đã tự động chuyển khoản (hoặc các trường hợp vượt hạn mức cần admin xem lại). |
| Thông báo | `/ThongBao` | Không đổi |
| **Phân quyền** | `/PhanQuyen` | **Đã bỏ vai trò "Kế toán"** (còn: Toàn quyền / Nhân viên hỗ trợ / Quản lý vận hành); **thêm cột "Lý do khoá"** hiển thị khi tài khoản bị khoá. |
| Báo cáo | `/BaoCao` | Không đổi |
| Nhật ký | `/NhatKy` | Không đổi (nay có thêm log của các hành động tự động) |

### Đã bỏ hoàn toàn (theo yêu cầu)
- Quản lý phương tiện (gộp vào Tài xế)
- Điểm đón/trả
- Quản lý giá (gộp vào Đăng chuyến)
- Khuyến mãi
- Quản lý nội dung
- Cấu hình hệ thống

## Việc cần làm tiếp (nối backend thật)
Xem từng comment `TODO BACKEND` đầu mỗi `*.cshtml.cs` — đó là các endpoint API gợi ý (REST) cần xây dựng để thay thế `SampleDataStore`. Gợi ý kiến trúc: tạo thêm project `XeGhepAdmin.Api` (ASP.NET Core Web API) hoặc controllers riêng, dùng Entity Framework Core kết nối DB thật, rồi inject các service đó thay cho `SampleDataStore` vào từng PageModel qua DI (đã đăng ký sẵn kiểu Singleton trong `Program.cs` — chỉ cần đổi kiểu đăng ký & interface).
