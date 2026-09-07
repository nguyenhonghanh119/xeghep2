using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET  /api/admin/bookings?status=&query=     -> danh sách yêu cầu đặt chỗ của khách trên từng chuyến
// - POST /api/admin/bookings/:id/cancel           -> nút "Huỷ hộ khách" (khi có tranh chấp)
public class DatXeModel : PageModel
{
    private readonly SampleDataStore _data;
    public DatXeModel(SampleDataStore data) => _data = data;

    public List<Booking> Bookings => _data.Bookings;

    public void OnGet()
    {
        ViewData["Title"] = "Quản lý đặt xe";
        ViewData["ActivePage"] = "dat-xe";
    }

    public static string PillClass(TrangThai t) => t switch
    {
        TrangThai.DaXacNhan => "approved",
        TrangThai.ChoDuyet => "pending",
        TrangThai.DaHuy => "cancelled",
        _ => "pending"
    };

    public static string PillText(TrangThai t) => t switch
    {
        TrangThai.DaXacNhan => "Đã xác nhận",
        TrangThai.ChoDuyet => "Chờ xác nhận",
        TrangThai.DaHuy => "Đã huỷ",
        _ => t.ToString()
    };
}
