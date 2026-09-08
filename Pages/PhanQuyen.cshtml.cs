using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// Đã tái cấu trúc phân quyền: BỎ vai trò "Kế toán" (nghiệp vụ hoa hồng/rút tiền nay xử lý tự động
// ở trang Thanh toán). Vai trò còn lại: Toàn quyền / Nhân viên hỗ trợ / Quản lý vận hành.
// MỚI: thêm cột "Lý do khoá" cho tài khoản quản trị viên bị khoá.
// TODO BACKEND:
// - GET    /api/admin/staff                -> danh sách tài khoản quản trị
// - POST   /api/admin/staff                  -> nút "+ Thêm quản trị viên"
// - PUT    /api/admin/staff/:id/role           -> đổi vai trò
// - PUT    /api/admin/staff/:id/lock            -> khoá kèm lý do
// - DELETE /api/admin/staff/:id                  -> xoá tài khoản
public class PhanQuyenModel : PageModel
{
    private readonly SampleDataStore _data;
    public PhanQuyenModel(SampleDataStore data) => _data = data;

    public List<StaffAccount> StaffAccounts => _data.StaffAccounts;

    public void OnGet()
    {
        ViewData["Title"] = "Phân quyền";
        ViewData["ActivePage"] = "phan-quyen";
    }

    public static string TenVaiTro(VaiTroQuanTri v) => v switch
    {
        VaiTroQuanTri.ToanQuyen => "Quản trị viên (Toàn quyền)",
        VaiTroQuanTri.NhanVienHoTro => "Nhân viên hỗ trợ",
        VaiTroQuanTri.QuanLyVanHanh => "Quản lý vận hành",
        _ => v.ToString()
    };
}
