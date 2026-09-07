using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET  /api/admin/reviews?rating=            -> tab "Đánh giá"
// - GET  /api/admin/complaints?status=          -> tab "Khiếu nại"
// - POST /api/admin/complaints/:id/resolve       -> nút "Đánh dấu đã xử lý"
public class DanhGiaModel : PageModel
{
    private readonly SampleDataStore _data;
    public DanhGiaModel(SampleDataStore data) => _data = data;

    public List<Review> Reviews => _data.Reviews;
    public List<Complaint> Complaints => _data.Complaints;

    public void OnGet()
    {
        ViewData["Title"] = "Đánh giá & khiếu nại";
        ViewData["ActivePage"] = "danh-gia";
    }
}
