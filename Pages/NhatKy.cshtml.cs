using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET /api/admin/activity-log?actor=&action=&from=&to=   -> bảng nhật ký thao tác quản trị
//   (bao gồm cả các thao tác do HỆ THỐNG TỰ ĐỘNG thực hiện, vd rút tiền tự động, tạo tài khoản tự động)
public class NhatKyModel : PageModel
{
    private readonly SampleDataStore _data;
    public NhatKyModel(SampleDataStore data) => _data = data;

    public List<NhatKyHoatDong> Logs => _data.ActivityLogs;

    public void OnGet()
    {
        ViewData["Title"] = "Nhật ký hoạt động";
        ViewData["ActivePage"] = "nhat-ky";
    }
}
