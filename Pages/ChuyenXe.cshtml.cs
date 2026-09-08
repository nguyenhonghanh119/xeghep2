using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET /api/admin/trips?status=&query=   -> danh sách tất cả chuyến xe do tài xế đăng
// - GET /api/admin/trips/:id                -> nút "Chi tiết"
public class ChuyenXeModel : PageModel
{
    private readonly SampleDataStore _data;
    public ChuyenXeModel(SampleDataStore data) => _data = data;

    public List<Trip> Trips => _data.Trips;

    public void OnGet()
    {
        ViewData["Title"] = "Quản lý chuyến xe";
        ViewData["ActivePage"] = "chuyen-xe";
    }

    public static string PillClass(TrangThai t) => t switch
    {
        TrangThai.SapKhoiHanh => "upcoming",
        TrangThai.DangChay => "running",
        TrangThai.HoanThanh => "done",
        TrangThai.DaHuy => "cancelled",
        _ => "pending"
    };

    public static string PillText(TrangThai t) => t switch
    {
        TrangThai.SapKhoiHanh => "Sắp khởi hành",
        TrangThai.DangChay => "Đang chạy",
        TrangThai.HoanThanh => "Hoàn thành",
        TrangThai.DaHuy => "Đã huỷ",
        _ => t.ToString()
    };
}
