using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - POST /api/admin/notifications/broadcast     -> nút "Gửi thông báo" (target: all|customers|drivers)
// - GET  /api/admin/notifications/history          -> bảng lịch sử bên dưới
public class ThongBaoModel : PageModel
{
    private readonly SampleDataStore _data;
    public ThongBaoModel(SampleDataStore data) => _data = data;

    public List<ThongBaoDaGui> LichSu => _data.ThongBaos;

    public void OnGet()
    {
        ViewData["Title"] = "Thông báo";
        ViewData["ActivePage"] = "thong-bao";
    }
}
