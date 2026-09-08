using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// MỚI — trang "Quản lý tài khoản": hiển thị các tài khoản khách hàng/tài xế được
// HỆ THỐNG TỰ ĐỘNG TẠO ngay khi người dùng tự đăng ký thành công qua app (không cần admin thao tác thủ công).
// TODO BACKEND:
// - GET /api/admin/accounts/auto-created?loai=&from=&to=  -> danh sách bên dưới
// - Sự kiện tạo tài khoản mới nên bắn realtime (webhook/queue) để danh sách này luôn cập nhật tức thời.
public class TaiKhoanModel : PageModel
{
    private readonly SampleDataStore _data;
    public TaiKhoanModel(SampleDataStore data) => _data = data;

    public List<TaiKhoanTuDongTao> DanhSach => _data.TaiKhoanTuDong
        .OrderByDescending(t => t.ThoiGianTao).ToList();

    public int TongKhachHangMoi => _data.TaiKhoanTuDong.Count(t => t.Loai == "Khách hàng");
    public int TongTaiXeMoi => _data.TaiKhoanTuDong.Count(t => t.Loai == "Tài xế");

    public void OnGet()
    {
        ViewData["Title"] = "Tài khoản mới";
        ViewData["ActivePage"] = "tai-khoan";
    }
}
