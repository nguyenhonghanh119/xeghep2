using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET  /api/admin/drivers?status=pending|active|locked&query=  -> dữ liệu 3 tab + tìm kiếm
//         (mỗi tài xế trả kèm luôn thông tin phương tiện — đã gộp, không còn endpoint /vehicles riêng)
// - GET  /api/admin/drivers/:id/documents   -> nút "Xem giấy tờ" (CCCD, GPLX, đăng ký xe, đăng kiểm, bảo hiểm)
// - POST /api/admin/drivers/:id/approve      -> nút "Duyệt hồ sơ" (duyệt luôn cả phương tiện đi kèm)
// - POST /api/admin/drivers/:id/reject        -> nút "Từ chối"
// - POST /api/admin/drivers/:id/suspend        -> nút "Khoá tài khoản"
// - POST /api/admin/drivers/:id/reactivate      -> nút "Mở khoá"
// - Cảnh báo tự động khi hạn đăng kiểm/bảo hiểm còn < 30 ngày (cron job hằng ngày) -> Vehicle.SapHetHan
public class TaiXeModel : PageModel
{
    private readonly SampleDataStore _data;
    public TaiXeModel(SampleDataStore data) => _data = data;

    public List<Driver> ChoDuyet => _data.Drivers.Where(d => d.TrangThai == TrangThai.ChoDuyet).ToList();
    public List<Driver> DangHoatDong => _data.Drivers.Where(d => d.TrangThai == TrangThai.DangHoatDong).ToList();
    public List<Driver> BiKhoa => _data.Drivers.Where(d => d.TrangThai == TrangThai.BiKhoa).ToList();

    public void OnGet()
    {
        ViewData["Title"] = "Tài xế";
        ViewData["ActivePage"] = "tai-xe";
    }
}
