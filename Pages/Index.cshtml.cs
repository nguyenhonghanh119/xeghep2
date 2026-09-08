using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET /api/admin/stats/overview        -> 4 stat-card đầu
// - GET /api/admin/stats/revenue-trend    -> dữ liệu biểu đồ doanh thu 7 ngày
// - GET /api/admin/drivers/pending         -> danh sách tài xế chờ duyệt (rút gọn)
// - GET /api/admin/activity/recent          -> hoạt động gần đây toàn hệ thống
public class IndexModel : PageModel
{
    private readonly SampleDataStore _data;
    public IndexModel(SampleDataStore data) => _data = data;

    public List<Driver> TaiXeChoDuyet => _data.Drivers.Where(d => d.TrangThai == TrangThai.ChoDuyet).ToList();
    public List<NhatKyHoatDong> HoatDongGanDay => _data.ActivityLogs.Take(4).ToList();
    public int TaiXeDangHoatDong => _data.Drivers.Count(d => d.TrangThai == TrangThai.DangHoatDong);
    public int SoHoSoChoDuyet => _data.SoHoSoTaiXeChoDuyet;

    public void OnGet()
    {
        ViewData["Title"] = "Dashboard";
        ViewData["ActivePage"] = "dashboard";
    }
}
