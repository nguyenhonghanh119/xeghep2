using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// Thay cho 2 trang cũ "Quản lý tuyến đường" (admin-tuyen-duong.php) và "Quản lý giá" (admin-gia.php):
// khi đăng 1 tuyến/chuyến mới, giá đề xuất được tính và hiển thị NGAY BÊN CẠNH (preview-sticky),
// dựa trên công thức giá mặc định — không cần chuyển sang màn hình khác để tra giá.
// TODO BACKEND:
// - GET    /api/admin/pricing                       -> công thức giá mặc định
// - PUT    /api/admin/pricing                        -> nút "Lưu cấu hình giá"
// - GET    /api/admin/routes?query=                  -> danh sách tuyến + giá riêng (nếu có)
// - POST   /api/admin/routes                          -> nút "Đăng chuyến" (tạo tuyến mới kèm giá)
// - PUT    /api/admin/routes/:id                        -> nút "Sửa"
// - DELETE /api/admin/routes/:id                         -> nút "Xoá"
public class DangChuyenModel : PageModel
{
    private readonly SampleDataStore _data;
    public DangChuyenModel(SampleDataStore data) => _data = data;

    public CauHinhGia GiaMacDinh => _data.GiaMacDinh;
    public List<TuyenGia> Tuyens => _data.Tuyens;

    public void OnGet()
    {
        ViewData["Title"] = "Đăng chuyến & Giá";
        ViewData["ActivePage"] = "dang-chuyen";
    }

    public string GiaHienThi(TuyenGia t) =>
        (t.GiaRiengMoiGhe ?? GiaMacDinh.TinhGiaDeXuat(t.KhoangCachKm)).ToString("N0") + "đ";
}
